using Colossal.IO.AssetDatabase;
using Game.Assets;
using CameraTimelapseMod.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static CameraTimelapseMod.Util.SaveTools;

namespace CameraTimelapseMod.Util
{

    public class FilteredSavesResult
    {
        public List<SaveEntry> Queue;
        public int StartIdx;          
        public bool HasResume;        
        public string ResumeError;  
        public int TotalBeforeFilter;
        public int AfterPrefix;
        public int AfterCity;
        public int AfterSkip;
        public int AfterMax;
    }
    public static class SaveTools
    {
        public class SaveEntry
        {
            public string Name;
            public DateTime LastModified;
            public object RawAsset;
            public string CityName;
        }

        public static List<SaveEntry> EnumerateAll()
        {
            var list = new List<SaveEntry>();
            try
            {
                foreach (var asset in AssetDatabase.user.GetAssets<SaveGameMetadata>())
                {
                    var entry = new SaveEntry { RawAsset = asset };

                    var t = asset.GetType();
                    entry.Name = (t.GetProperty("name")?.GetValue(asset) as string) ?? "Unknown";

                    var path = t.GetProperty("path")?.GetValue(asset) as string;
                    entry.LastModified = (!string.IsNullOrEmpty(path) && File.Exists(path))
                        ? File.GetLastWriteTime(path)
                        : DateTime.MinValue;

                    entry.CityName = ReadCityName(asset);

                    list.Add(entry);
                    LogsTools.Info($"Save {entry.Name}: city='{entry.CityName}'");
                }
            }
            catch (Exception ex)
            {
                LogsTools.Error($"EnumerateAll failed: {ex}");
            }
            return list;
        }
        private static string ReadCityName(SaveGameMetadata meta)
        {
            try
            {
                var targetProp = typeof(SaveGameMetadata).GetProperty(
                    "target",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (targetProp == null) return "";

                var saveInfo = targetProp.GetValue(meta) as SaveInfo;
                return saveInfo?.cityName ?? "";
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"ReadCityName failed for {meta?.name}: {ex.Message}");
                return "";
            }
        }

        public static IEnumerable<SaveEntry> Apply(IEnumerable<SaveEntry> all, FilterOpts opts)
        {
            var q = all;
            if (!string.IsNullOrEmpty(opts.Prefix))
                q = q.Where(s => s.Name.StartsWith(opts.Prefix, StringComparison.OrdinalIgnoreCase));
            if (opts.BeforeDate.HasValue)
                q = q.Where(s => s.LastModified <= opts.BeforeDate.Value);
            if (opts.AfterDate.HasValue)
                q = q.Where(s => s.LastModified >= opts.AfterDate.Value);
            if (opts.Ignore != null && opts.Ignore.Count > 0)
                q = q.Where(s => !opts.Ignore.Contains(s.Name));

            return q.ToList();
        }

        public static FilteredSavesResult BuildQueueFromSettings(bool ignoreResumeFromSaveName = false)
        {
            var setting = Mod.Setting;
            var result = new FilteredSavesResult
            {
                Queue = new List<SaveEntry>(),
                StartIdx = 0,
                HasResume = false,
                ResumeError = null
            };

            var all = EnumerateAll().ToList();
            result.TotalBeforeFilter = all.Count;

            var opts = new FilterOpts
            {
                Prefix = setting.SavePrefix,
                MaxCount = 0
            };
            var filtered = Apply(all, opts).ToList();
            result.AfterPrefix = filtered.Count;

            var sorted = (setting.SaveSortOrder == Setting.SortOrder.DescendingDate
                    ? filtered.OrderByDescending(e => e.LastModified)
                    : filtered.OrderBy(e => e.LastModified))
                .ToList();

            string cityFilter = (setting.CityNameFilter ?? "").Trim();
            if (!string.IsNullOrEmpty(cityFilter))
            {
                sorted = sorted
                    .Where(e => !string.IsNullOrEmpty(e.CityName) &&
                                e.CityName.IndexOf(cityFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }
            result.AfterCity = sorted.Count;

            int resumeIdx = -1;
            if (!ignoreResumeFromSaveName)
            {
                string resumeName = (setting.ResumeFromSaveName ?? "").Trim();
                if (!string.IsNullOrEmpty(resumeName))
                {
                    resumeIdx = sorted.FindIndex(e =>
                        string.Equals(e.Name, resumeName, StringComparison.OrdinalIgnoreCase));
                    if (resumeIdx < 0)
                    {
                        resumeIdx = sorted.FindIndex(e =>
                            e.Name.IndexOf(resumeName, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    if (resumeIdx < 0)
                    {
                        result.ResumeError =
                            $"Save '{resumeName}' not found in the {sorted.Count} filtered saves. " +
                            "Check that your filters (Prefix, CityNameFilter) include this save.";
                    }
                    else
                    {
                        sorted = sorted.GetRange(resumeIdx, sorted.Count - resumeIdx);
                        resumeIdx = 0;   
                        result.HasResume = true;
                    }
                }
            }

            int skipBetween = setting.SkipBetweenSavesInt;
            if (skipBetween > 0 && sorted.Count > 0)
            {
                int stride = skipBetween + 1;
                var thinned = new List<SaveEntry>();
                for (int i = 0; i < sorted.Count; i += stride)
                    thinned.Add(sorted[i]);

                // Toujours inclure la dernière save de la liste filtrée
                var last = sorted[sorted.Count - 1];
                if (thinned.Count == 0 || thinned[thinned.Count - 1] != last)
                {
                    thinned.Add(last);
                }

                sorted = thinned;
            }
            result.AfterSkip = sorted.Count;

            if (setting.MaxSaves > 0 && sorted.Count > setting.MaxSaves)
            {
                sorted = sorted.Take(setting.MaxSaves).ToList();
            }
            result.AfterMax = sorted.Count;

            result.Queue = sorted;
            result.StartIdx = 0;

            return result;
        }
    }
}