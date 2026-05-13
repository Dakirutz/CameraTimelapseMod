using CameraTimelapseMod.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CameraTimelapseMod.Util
{
    internal class GameTools
    {

        private static readonly ComponentType[] _visibleNetworkTypes = new ComponentType[]
        {
            ComponentType.ReadOnly<Game.Net.Road>(),
            ComponentType.ReadOnly<Game.Net.TramTrack>(),
            ComponentType.ReadOnly<Game.Net.TrainTrack>(),
            ComponentType.ReadOnly<Game.Net.SubwayTrack>(),
        };

        private static bool IsUnderground(EntityManager em, Entity edge)
        {
            if (!em.HasComponent<Game.Net.Elevation>(edge)) return false;
            var elev = em.GetComponentData<Game.Net.Elevation>(edge).m_Elevation;
            return elev.x < -0.5f && elev.y < -0.5f;
        }

        public static void ApplyTimeAndWeather(float captureTime, bool forceClearWeather)
        {
            try
            {
                ApplyTime(captureTime);
                if (forceClearWeather) ApplyClearWeather();
            }
            catch (Exception ex) { LogsTools.Error($"TimeWeather.Apply: {ex}"); }
        }

        public static void RestoreWeather()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                var planetary = world?.GetExistingSystemManaged<Game.Simulation.PlanetarySystem>();
                if (planetary != null && planetary.overrideTime)
                {
                    planetary.overrideTime = false;
                    LogsTools.Info("Restored simulated day/night cycle");
                }

                var clim = world?.GetExistingSystemManaged<Game.Simulation.ClimateSystem>();
                if (clim != null)
                {
                    clim.precipitation.overrideState = false;
                    clim.cloudiness.overrideState = false;
                    clim.fog.overrideState = false;
                }
            }
            catch (Exception ex) { LogsTools.Warn($"TimeWeather.Restore: {ex.Message}"); }
        }

        public static void ApplyTime(float captureTime)
        {
            var planetary = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<Game.Simulation.PlanetarySystem>();
            if (planetary == null) { LogsTools.Warn("PlanetarySystem not found"); return; }

            planetary.overrideTime = true;
            planetary.time = captureTime;   

            LogsTools.Info($"Forced time to {captureTime:F2}h (overrideTime=true)");
        }

        public static void ApplyClearWeather()
        {
            var clim = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<Game.Simulation.ClimateSystem>();
            if (clim == null) return;

            try
            {
                clim.precipitation.overrideState = true;
                clim.precipitation.overrideValue = 0f;
                clim.cloudiness.overrideState = true;
                clim.cloudiness.overrideValue = 0f;
                clim.fog.overrideState = true;
                clim.fog.overrideValue = 0f;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"Could not override weather: {ex.Message}");
            }
        }

        public class RecentEdgesResult
        {
            public List<Entity> Countable = new List<Entity>();  
            public List<Entity> Bonus = new List<Entity>();      
            public List<Entity> All => Countable.Concat(Bonus).ToList();
        }

        public static RecentEdgesResult GetMostRecentEdges(int countableTarget)
        {
            var result = new RecentEdgesResult();
            if (countableTarget <= 0) return result;

            EntityManager? entityManager = GetEntityManager();
            if (entityManager == null) return result;
            EntityManager em = entityManager.Value;

            string districtFilter = (Mod.Setting?.AutoModDistrictFilter ?? "").Trim();
            var resolvedDistricts = ResolveDistrictsFromFilter(districtFilter);
            var districtEntities = resolvedDistricts.Select(d => d.Entity).ToList();
            bool hasFilter = !string.IsNullOrEmpty(districtFilter);

            if (hasFilter && districtEntities.Count == 0)
            {
                LogsTools.Warn(
                    $"GetMostRecentEdges: no valid districts in filter '{districtFilter}'.");
                return result;
            }
            if (hasFilter)
            {
                LogsTools.Info(
                    $"GetMostRecentEdges: filtering by {districtEntities.Count} district(s) in priority order: " +
                    string.Join(" > ", resolvedDistricts.Select(d => $"'{d.Name}'")));
            }

            var query = em.CreateEntityQuery(GetAllEdgesQueryDesc());
            var allEntities = query.ToEntityArray(Allocator.Temp);
            var allOrders = query.ToComponentDataArray<Game.Net.BuildOrder>(Allocator.Temp);

            try
            {
                var allIndexed = new List<(Entity e, uint order, bool isCountable, int priority)>(allEntities.Length);

                for (int i = 0; i < allEntities.Length; i++)
                {
                    var entity = allEntities[i];

                    int priority = hasFilter
                        ? GetEdgeFirstDistrictIndex(entity, districtEntities)
                        : 0;
                    if (priority < 0) continue;

                    bool countable = IsVisibleNetwork(em, entity) && !IsUnderground(em, entity);
                    allIndexed.Add((entity, allOrders[i].m_Start, countable, priority));
                }

                if (hasFilter)
                    LogsTools.Info($"District filter: {allIndexed.Count}/{allEntities.Length} edges matched");

                allIndexed.Sort((a, b) =>
                {
                    int cmp = a.priority.CompareTo(b.priority);
                    return cmp != 0 ? cmp : b.order.CompareTo(a.order);
                });

                for (int i = 0; i < allIndexed.Count; i++)
                {
                    var item = allIndexed[i];
                    if (item.isCountable)
                    {
                        if (result.Countable.Count >= countableTarget) break;
                        result.Countable.Add(item.e);
                    }
                    else if (result.Countable.Count < countableTarget)
                    {
                        result.Bonus.Add(item.e);
                    }
                }

                LogsTools.Info(
                    $"GetMostRecentEdges: target {countableTarget}, " +
                    $"got {result.Countable.Count} countable + {result.Bonus.Count} bonus");
            }
            finally
            {
                allEntities.Dispose();
                allOrders.Dispose();
                query.Dispose();
            }

            return result;
        }

        public static int GetEdgeFirstDistrictIndex(Entity edge, List<Entity> orderedDistricts)
        {
            if (orderedDistricts == null || orderedDistricts.Count == 0) return 0;

            EntityManager? entityManager = GetEntityManager();
            if (entityManager == null) return -1;
            EntityManager em = entityManager.Value;

            var edgeDistricts = new HashSet<Entity>();

            if (em.HasComponent<Game.Areas.CurrentDistrict>(edge))
                edgeDistricts.Add(em.GetComponentData<Game.Areas.CurrentDistrict>(edge).m_District);

            if (em.HasComponent<Game.Net.Edge>(edge))
            {
                var ed = em.GetComponentData<Game.Net.Edge>(edge);
                if (em.HasComponent<Game.Areas.CurrentDistrict>(ed.m_Start))
                    edgeDistricts.Add(em.GetComponentData<Game.Areas.CurrentDistrict>(ed.m_Start).m_District);
                if (em.HasComponent<Game.Areas.CurrentDistrict>(ed.m_End))
                    edgeDistricts.Add(em.GetComponentData<Game.Areas.CurrentDistrict>(ed.m_End).m_District);
            }

            if (em.HasBuffer<Game.Buildings.ConnectedBuilding>(edge))
            {
                var buf = em.GetBuffer<Game.Buildings.ConnectedBuilding>(edge);
                for (int i = 0; i < buf.Length; i++)
                {
                    var b = buf[i].m_Building;
                    if (em.Exists(b) && em.HasComponent<Game.Areas.CurrentDistrict>(b))
                        edgeDistricts.Add(em.GetComponentData<Game.Areas.CurrentDistrict>(b).m_District);
                }
            }

            for (int i = 0; i < orderedDistricts.Count; i++)
            {
                if (edgeDistricts.Contains(orderedDistricts[i]))
                    return i;
            }

            if (TryGetEdgeMidpoint(em, edge, out float3 mid))
            {
                for (int i = 0; i < orderedDistricts.Count; i++)
                {
                    if (IsPointInDistrict(em, mid, orderedDistricts[i]))
                        return i;
                }
            }

            return -1;
        }

        private static bool TryGetEdgeMidpoint(EntityManager em, Entity edge, out float3 midpoint)
        {
            midpoint = default;
            if (!em.HasComponent<Game.Net.Edge>(edge)) return false;
            var ed = em.GetComponentData<Game.Net.Edge>(edge);

            if (!em.HasComponent<Game.Net.Node>(ed.m_Start)) return false;
            if (!em.HasComponent<Game.Net.Node>(ed.m_End)) return false;

            float3 a = em.GetComponentData<Game.Net.Node>(ed.m_Start).m_Position;
            float3 b = em.GetComponentData<Game.Net.Node>(ed.m_End).m_Position;
            midpoint = (a + b) * 0.5f;
            return true;
        }

        public static void DestroyEdgesAndAllAdjacentBuildings(
            List<Entity> edges)
        {
            if (edges == null || edges.Count == 0) return;

            int destroyedBuildings = 0;
            int destroyedEdges = 0;

            var buildings = CollectBuildingsAdjacentToEdges(edges);
            foreach (var b in buildings)
            {
                if (DestroyBuilding(b)) destroyedBuildings++;
            }

            foreach (var edge in edges)
            {
                if (DestroyEdgeWithNodeUpdate(edge)) destroyedEdges++;
            }

            LogsTools.Info(
                $"DestroyEdgesAndAllAdjacentBuildings: " +
                $"{destroyedEdges} edges, {destroyedBuildings} buildings destroyed.");
        }


        public static void PreviewRecentAsConstruction(int recentEdgesCount)
        {
            if (recentEdgesCount <= 0) return;

            var recent = GetMostRecentEdges(recentEdgesCount);
            if (recent.Countable.Count == 0 && recent.Bonus.Count == 0) return;

            LogsTools.Info(
                $"PreviewRecentAsConstruction: marking buildings around " +
                $"{recent.Countable.Count} countable edges (no destruction)");

            MarkBuildingsAroundEdgesAsConstruction(recent.Countable,false);
        }


        public static void MarkBuildingsAroundEdgesAsConstruction(
            List<Entity> edges, bool destroySpecialBuildings)
        {
            EntityManager? entityManager = GetEntityManager();
            if (entityManager == null) return;
            EntityManager em = entityManager.Value;

            if (edges == null || edges.Count == 0) return;

            int marked = 0;
            int destroyedSpecial = 0;
            int skippedSpecial = 0;

            var buildings = CollectBuildingsAdjacentToEdges(edges);

            foreach (var b in buildings)
            {
                if (!em.Exists(b)) continue;
                if (em.HasComponent<Game.Common.Deleted>(b)) continue;

                if (IsSpecialBuilding(b))
                {
                    if (destroySpecialBuildings)
                    {
                        if (DestroyBuilding(b)) destroyedSpecial++;
                    }
                    else
                    {
                        skippedSpecial++;
                    }
                }
                else
                {
                    if (SetBuildingToConstruction(b)) marked++;
                }
            }

            LogsTools.Info(
                $"MarkBuildingsAroundEdgesAsConstruction: " +
                $"{marked} marked as construction, {destroyedSpecial} special buildings destroyed.");
        }


        private static void UnhideAllSubObjects(EntityManager em, Entity parent)
        {
            if (!em.HasBuffer<Game.Objects.SubObject>(parent)) return;

            var subObjects = em.GetBuffer<Game.Objects.SubObject>(parent);
            for (int i = 0; i < subObjects.Length; i++)
            {
                var sub = subObjects[i].m_SubObject;
                if (!em.Exists(sub)) continue;

                try
                {
                    if (em.HasComponent<Game.Tools.Hidden>(sub))
                        em.RemoveComponent<Game.Tools.Hidden>(sub);
                    UnhideAllSubObjects(em, sub);
                }
                catch { }
            }
        }

        private static void UnhideSubObjects(EntityManager em, Entity parent)
        {
            if (!em.HasBuffer<Game.Objects.SubObject>(parent)) return;

            var subObjects = em.GetBuffer<Game.Objects.SubObject>(parent);
            for (int i = 0; i < subObjects.Length; i++)
            {
                var sub = subObjects[i].m_SubObject;
                if (!em.Exists(sub)) continue;

                try
                {
                    if (em.HasComponent<Game.Tools.Hidden>(sub))
                        em.RemoveComponent<Game.Tools.Hidden>(sub);
                    UnhideSubObjects(em, sub);
                }
                catch { }
            }
        }

        public static EntityQueryDesc GetAllEdgesQueryDesc()
        {
            return new EntityQueryDesc
            {
                All = new ComponentType[]
                {
            ComponentType.ReadOnly<Game.Net.Edge>(),
            ComponentType.ReadOnly<Game.Net.BuildOrder>()
                },
                None = new ComponentType[]
                {
            ComponentType.ReadOnly<Game.Common.Deleted>(),
            ComponentType.ReadOnly<Game.Common.Owner>() 
                }
            };
        }

        public static EntityQueryDesc GetVisibleEdgesQueryDesc()
        {
            var desc = GetAllEdgesQueryDesc();
            desc.Any = _visibleNetworkTypes;
            return desc;
        }

        private static bool IsVisibleNetwork(EntityManager em, Entity edge)
        {
            foreach (var t in _visibleNetworkTypes)
            {
                if (em.HasComponent(edge, t)) return true;
            }
            return false;
        }

        public static HashSet<Entity> CollectBuildingsAdjacentToEdges(
            List<Entity> edges)
        {
            var buildings = new HashSet<Entity>();
            EntityManager? entityManager = GetEntityManager();
            if (entityManager == null) return buildings;
            EntityManager em = entityManager.Value;

            foreach (var edge in edges)
            {
                if (!em.Exists(edge)) continue;
                if (!em.HasBuffer<Game.Buildings.ConnectedBuilding>(edge)) continue;

                var buf = em.GetBuffer<Game.Buildings.ConnectedBuilding>(edge);
                for (int i = 0; i < buf.Length; i++)
                {
                    var b = buf[i].m_Building;
                    if (em.Exists(b))
                        buildings.Add(b);
                }
            }

            return buildings;
        }


        public static bool IsSpecialBuilding(Entity building)
        {
            try
            {
                EntityManager? entityManager = GetEntityManager();
                if (entityManager == null) return false;
                EntityManager em = entityManager.Value;

                if (!em.HasComponent<Game.Prefabs.PrefabRef>(building))
                    return false;

                var prefabRef = em.GetComponentData<Game.Prefabs.PrefabRef>(building);
                Entity prefab = prefabRef.m_Prefab;
                if (!em.Exists(prefab))
                    return false;

                if (em.HasComponent<Game.Prefabs.SignatureBuildingData>(prefab))
                    return true;

                if (!em.HasComponent<Game.Prefabs.SpawnableBuildingData>(prefab))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool DestroyBuilding(Entity building)
        {
            try
            {
                EntityManager? entityManager = GetEntityManager();
                if (entityManager == null) return false;
                EntityManager em = entityManager.Value;

                if (!em.Exists(building)) return false;
                if (em.HasComponent<Game.Common.Deleted>(building)) return false;

                em.AddComponent<Game.Common.Deleted>(building);
                return true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"DestroyBuilding({building.Index}) failed: {ex.Message}");
                return false;
            }
        }

        public static bool DestroyEdgeWithNodeUpdate(Entity edge)
        {
            try
            {
                EntityManager? entityManager = GetEntityManager();
                if (entityManager == null) return false;
                EntityManager em = entityManager.Value;

                if (!em.Exists(edge)) return false;
                if (em.HasComponent<Game.Common.Deleted>(edge)) return false;

                var edgeData = em.GetComponentData<Game.Net.Edge>(edge);
                Entity startNode = edgeData.m_Start;
                Entity endNode = edgeData.m_End;

                em.AddComponent<Game.Common.Deleted>(edge);

                TryDestroyNodeIfOrphan(em, startNode, edge);
                TryDestroyNodeIfOrphan(em, endNode, edge);

                AddUpdatedSafely(em, startNode);
                AddUpdatedSafely(em, endNode);

                return true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"DestroyEdge({edge.Index}) failed: {ex.Message}");
                return false;
            }
        }

        // NOTE: on utilise em.AddComponent direct (pas ECB).
        // L'ECB diffère les writes au Playback, donc TryDestroyNodeIfOrphan
        // ne verrait pas les edges précédentes comme Deleted dans le même step
        // → mauvais comptage → nodes orphelins mal gérés → crash électrique ElectricityFlowEdge .
        public static void TryDestroyNodeIfOrphan(EntityManager em, Entity node, Entity edgeBeingDeleted)
        {
            if (!em.Exists(node)) return;
            if (em.HasComponent<Game.Common.Deleted>(node)) return;
            if (!em.HasBuffer<Game.Net.ConnectedEdge>(node)) return;

            var connected = em.GetBuffer<Game.Net.ConnectedEdge>(node);
            int activeEdges = 0;
            for (int i = 0; i < connected.Length; i++)
            {
                var e = connected[i].m_Edge;
                if (e == edgeBeingDeleted) continue;
                if (!em.Exists(e)) continue;
                if (em.HasComponent<Game.Common.Deleted>(e)) continue;
                activeEdges++;
            }

            if (activeEdges == 0)
            {
                try
                {
                    em.AddComponent<Game.Common.Deleted>(node);
                    LogsTools.Info($"Destroyed orphan node {node.Index}");
                }
                catch { }
            }
        }




        public static void AddUpdatedSafely(EntityManager em, Entity entity)
        {
            if (!em.Exists(entity)) return;
            if (em.HasComponent<Game.Common.Deleted>(entity)) return;
            if (em.HasComponent<Game.Common.Updated>(entity)) return;

            try { em.AddComponent<Game.Common.Updated>(entity); }
            catch { }
        }


        public static bool SetBuildingToConstruction(Entity building)
        {
            try
            {
                EntityManager? entityManager = GetEntityManager();
                if (entityManager == null) return false;
                EntityManager em = entityManager.Value;

                if (!em.Exists(building)) return false;
                if (em.HasComponent<Game.Common.Deleted>(building)) return false;
                if (em.HasComponent<Game.Objects.UnderConstruction>(building)) return false;

                Entity foundCrane = Entity.Null;
                if (em.HasBuffer<Game.Objects.SubObject>(building))
                {
                    var subs = em.GetBuffer<Game.Objects.SubObject>(building);
                    for (int i = 0; i < subs.Length; i++)
                    {
                        var sub = subs[i].m_SubObject;
                        if (em.Exists(sub) && em.HasComponent<Game.Objects.Crane>(sub))
                        {
                            foundCrane = sub;
                            break;
                        }
                    }
                }

                Entity targetPrefab = Entity.Null;
                //if (em.HasComponent<Game.Prefabs.PrefabRef>(building))
               // {
               // this make the building stay itself not showing construction.     targetPrefab = em.GetComponentData<Game.Prefabs.PrefabRef>(building).m_Prefab;
               // }

                using (var ecb = new EntityCommandBuffer(Allocator.Temp))
                {
                    HideSubObjectsExceptCrane(ecb, em, building, foundCrane);

                    if (foundCrane != Entity.Null && em.HasComponent<Game.Tools.Hidden>(foundCrane))
                        ecb.RemoveComponent<Game.Tools.Hidden>(foundCrane);

                    ecb.AddComponent(building, new Game.Objects.UnderConstruction
                    {
                        m_NewPrefab = targetPrefab,
                        m_Progress = 1,
                        m_Speed = 50
                    });

                    if (!em.HasComponent<Game.Common.Updated>(building))
                        ecb.AddComponent<Game.Common.Updated>(building);

                    ecb.Playback(em);
                }

                return true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"SetBuildingToConstruction({building.Index}) failed: {ex.Message}");
                return false;
            }
        }

        private static void HideSubObjectsExceptCrane(
            EntityCommandBuffer ecb, EntityManager em, Entity parent, Entity craneToKeep)
        {
            if (!em.HasBuffer<Game.Objects.SubObject>(parent)) return;

            var subObjects = em.GetBuffer<Game.Objects.SubObject>(parent);
            for (int i = 0; i < subObjects.Length; i++)
            {
                var sub = subObjects[i].m_SubObject;
                if (!em.Exists(sub)) continue;
                if (sub == craneToKeep) continue;
                if (em.HasComponent<Game.Tools.Hidden>(sub)) continue;

                ecb.AddComponent<Game.Tools.Hidden>(sub);
                HideSubObjectsExceptCrane(ecb, em, sub, craneToKeep);
            }
        }
        private static void HideSubObjects(Entity parent) 
        {
            EntityManager? entityManager = GetEntityManager();
            if (entityManager == null) return;
            EntityManager em = entityManager.Value;

            if (!em.HasBuffer<Game.Objects.SubObject>(parent)) return;

            var subObjects = em.GetBuffer<Game.Objects.SubObject>(parent);
            for (int i = 0; i < subObjects.Length; i++)
            {
                var sub = subObjects[i].m_SubObject;
                if (!em.Exists(sub)) continue;
                if (em.HasComponent<Game.Tools.Hidden>(sub)) continue;

                try
                {
                    em.AddComponent<Game.Tools.Hidden>(sub);
                    HideSubObjects(sub);
                }
                catch { }
            }
        }



        public static void SetSimulationSpeed(int speed)
        {
            try
            {
                var sim = World.DefaultGameObjectInjectionWorld?
                    .GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
                if (sim == null) return;
                sim.selectedSpeed = (float)speed;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"SetSimulationSpeed({speed}) failed: {ex.Message}");
            }
        }

        public static bool SetSimulationPaused(bool paused)
        {
            try
            {
                var sim = World.DefaultGameObjectInjectionWorld?
                    .GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
                if (sim == null) return false;

                bool wasPaused = (sim.selectedSpeed == 0f);

                if (paused)
                    sim.selectedSpeed = 0f;
                else if (sim.selectedSpeed == 0f)
                    sim.selectedSpeed = 1f; 

                return wasPaused;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"SetSimulationPaused({paused}) failed: {ex.Message}");
                return false;
            }
        }


        public static bool IsSimulationPaused()
        {
            try
            {
                var sim = World.DefaultGameObjectInjectionWorld?
                    .GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
                return sim != null && sim.selectedSpeed == 0f;
            }
            catch
            {
                return false;
            }
        }

        public static EntityManager? GetEntityManager()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                LogsTools.Warn("No DefaultGameObjectInjectionWorld available");
                return null;
            }
            return world.EntityManager;
        }
        public static bool TryGetEdgePosition(Entity edge, out Vector3 position)
        {
            position = default;
            try
            {
                EntityManager? entityManager = GetEntityManager();
                if (entityManager == null) return false;
                EntityManager em = entityManager.Value;
                if (!em.Exists(edge)) return false;
                if (!em.HasComponent<Game.Net.Edge>(edge)) return false;

                var edgeData = em.GetComponentData<Game.Net.Edge>(edge);
                if (!em.HasComponent<Game.Net.Node>(edgeData.m_Start)) return false;

                var node = em.GetComponentData<Game.Net.Node>(edgeData.m_Start);
                position = node.m_Position;
                return true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"TryGetEdgePosition({edge.Index}) failed: {ex.Message}");
                return false;
            }
        }



        public static Entity GetEdgeDistrict(EntityManager em, Entity edge)
        {
            try
            {
                if (!em.Exists(edge)) return Entity.Null;

                if (em.HasComponent<Game.Areas.CurrentDistrict>(edge))
                    return em.GetComponentData<Game.Areas.CurrentDistrict>(edge).m_District;

                if (!em.HasComponent<Game.Net.Edge>(edge)) return Entity.Null;
                var ed = em.GetComponentData<Game.Net.Edge>(edge);

                if (em.HasComponent<Game.Areas.CurrentDistrict>(ed.m_Start))
                    return em.GetComponentData<Game.Areas.CurrentDistrict>(ed.m_Start).m_District;

                if (em.HasComponent<Game.Areas.CurrentDistrict>(ed.m_End))
                    return em.GetComponentData<Game.Areas.CurrentDistrict>(ed.m_End).m_District;

                return Entity.Null;
            }
            catch
            {
                return Entity.Null;
            }
        }


        public static bool IsEdgeFullyInDistrict(EntityManager em, Entity edge, Entity district)
        {
            if (!em.Exists(edge)) return false;
            if (!em.HasComponent<Game.Net.Edge>(edge)) return false;

            var ed = em.GetComponentData<Game.Net.Edge>(edge);

            Entity startD = Entity.Null;
            Entity endD = Entity.Null;

            if (em.HasComponent<Game.Areas.CurrentDistrict>(ed.m_Start))
                startD = em.GetComponentData<Game.Areas.CurrentDistrict>(ed.m_Start).m_District;

            if (em.HasComponent<Game.Areas.CurrentDistrict>(ed.m_End))
                endD = em.GetComponentData<Game.Areas.CurrentDistrict>(ed.m_End).m_District;

            return startD == district && endD == district;
        }

        public static List<(string Name, Entity Entity)> ResolveDistrictsFromFilter(string commaSeparatedNames)
        {
            var result = new List<(string, Entity)>();
            if (string.IsNullOrEmpty(commaSeparatedNames)) return result;

            EntityManager? entityManager = GetEntityManager();
            if (entityManager == null) return result;
            EntityManager em = entityManager.Value;

            var names = commaSeparatedNames
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var name in names)
            {
                var entity = FindDistrictByName(em, name);
                if (entity != Entity.Null)
                {
                    result.Add((name, entity));
                    LogsTools.Info($"District '{name}' resolved to entity #{entity.Index}");
                }
                else
                {
                    LogsTools.Warn($"District '{name}' not found, skipped");
                }
            }
            return result;
        }
        public static Entity FindDistrictByName(EntityManager em, string name)
        {
            if (string.IsNullOrEmpty(name)) return Entity.Null;
            name = name.Trim();

            var query = em.CreateEntityQuery(ComponentType.ReadOnly<Game.Areas.District>());
            try
            {
                var districts = query.ToEntityArray(Allocator.Temp);
                try
                {
                    foreach (var d in districts)
                    {
                        string dname = GetDistrictName(em, d);
                        if (string.Equals(dname, name, StringComparison.OrdinalIgnoreCase))
                            return d;
                    }
                    foreach (var d in districts)
                    {
                        string dname = GetDistrictName(em, d);
                        if (!string.IsNullOrEmpty(dname) &&
                            dname.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                            return d;
                    }
                }
                finally { districts.Dispose(); }
            }
            finally { query.Dispose(); }

            return Entity.Null;
        }
        public static bool IsEdgeInAnyDistrict(Entity edge, List<Entity> districts) => GetEdgeFirstDistrictIndex(edge, districts) >= 0;

        public static void DebugCountEdgesPerDistrict()
        {
            EntityManager? entityManager = GameTools.GetEntityManager();
            if (entityManager == null) { UITools.ShowError("No world loaded."); return; }
            EntityManager em = entityManager.Value;

            string filter = (Mod.Setting?.AutoModDistrictFilter ?? "").Trim();
            if (string.IsNullOrEmpty(filter))
            {
                UITools.ShowError("No district filter set...");
                return;
            }

            var resolved = GameTools.ResolveDistrictsFromFilter(filter);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"District filter: '{filter}'");
            sb.AppendLine($"Resolved: {resolved.Count} district(s)");
            sb.AppendLine();

            var query = em.CreateEntityQuery(GameTools.GetAllEdgesQueryDesc());
            var allEntities = query.ToEntityArray(Allocator.Temp);
            try
            {
                sb.AppendLine($"Total edges in world: {allEntities.Length}");
                sb.AppendLine();

                foreach (var (name, district) in resolved)
                {
                    int matchEdge = 0, matchStart = 0, matchEnd = 0;
                    int matchGeometric = 0;
                    int matchFully = 0, matchAny = 0, matchBuilding = 0, matchUnion = 0;
                    var single = new List<Entity> { district };

                    foreach (var edge in allEntities)
                    {
                        bool eHas = em.HasComponent<Game.Areas.CurrentDistrict>(edge) &&
                            em.GetComponentData<Game.Areas.CurrentDistrict>(edge).m_District == district;
                        if (eHas) matchEdge++;

                        bool startHas = false, endHas = false;
                        if (em.HasComponent<Game.Net.Edge>(edge))
                        {
                            var ed = em.GetComponentData<Game.Net.Edge>(edge);
                            startHas = em.HasComponent<Game.Areas.CurrentDistrict>(ed.m_Start) &&
                                em.GetComponentData<Game.Areas.CurrentDistrict>(ed.m_Start).m_District == district;
                            endHas = em.HasComponent<Game.Areas.CurrentDistrict>(ed.m_End) &&
                                em.GetComponentData<Game.Areas.CurrentDistrict>(ed.m_End).m_District == district;
                        }
                        if (startHas) matchStart++;
                        if (endHas) matchEnd++;
                        if (startHas && endHas) matchFully++;
                        if (startHas || endHas) matchAny++;

                        bool buildingHas = false;
                        if (em.HasBuffer<Game.Buildings.ConnectedBuilding>(edge))
                        {
                            var buf = em.GetBuffer<Game.Buildings.ConnectedBuilding>(edge);
                            for (int i = 0; i < buf.Length && !buildingHas; i++)
                            {
                                var b = buf[i].m_Building;
                                if (em.Exists(b) &&
                                    em.HasComponent<Game.Areas.CurrentDistrict>(b) &&
                                    em.GetComponentData<Game.Areas.CurrentDistrict>(b).m_District == district)
                                {
                                    buildingHas = true;
                                }
                            }
                        }
                        if (buildingHas) matchBuilding++;

                        if (GameTools.TryGetEdgeMidpoint(em, edge, out var mid))
                        {
                            if (GameTools.IsPointInDistrict(em, mid, district))
                                matchGeometric++;
                        }

                        if (GameTools.IsEdgeInAnyDistrict(edge, single)) matchUnion++;
                    }

                    sb.AppendLine($"  '{name}' (#{district.Index}):");
                    sb.AppendLine($"    edge.CurrentDistrict      : {matchEdge}");
                    sb.AppendLine($"    start node                : {matchStart}");
                    sb.AppendLine($"    end node                  : {matchEnd}");
                    sb.AppendLine($"    both nodes (FULLY)        : {matchFully}");
                    sb.AppendLine($"    any node                  : {matchAny}");
                    sb.AppendLine($"    via connected building    : {matchBuilding}");
                    sb.AppendLine($"    UNION (used in practice)  : {matchUnion}");
                    sb.AppendLine($"    via geometric midpoint    : {matchGeometric}");
                    sb.AppendLine();
                }
            }
            finally
            {
                allEntities.Dispose();
                query.Dispose();
            }

            string msg = sb.ToString();
            LogsTools.Info(msg);
            UITools.ShowMessage("Edges per district", msg);
        }

        public static bool IsPointInDistrict(EntityManager em, Vector3 point, Entity district)
        {
            if (!em.Exists(district)) return false;
            if (!em.HasBuffer<Game.Areas.Node>(district)) return false;

            var nodes = em.GetBuffer<Game.Areas.Node>(district);
            if (nodes.Length < 3) return false;

            return PointInPolygonXZ(point, nodes);
        }

        private static bool PointInPolygonXZ(
            Vector3 p,
            DynamicBuffer<Game.Areas.Node> nodes)
        {
            bool inside = false;
            int n = nodes.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector3 a = nodes[i].m_Position;
                Vector3 b = nodes[j].m_Position;

                bool crosses = ((a.z > p.z) != (b.z > p.z)) &&
                               (p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x);
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static float Sign(float3 p, float3 a, float3 b)
        {
            return (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.z - b.z);
        }

        public static string GetDistrictName(EntityManager em, Entity district)
        {
            try
            {
                var nameSystem = World.DefaultGameObjectInjectionWorld
                    ?.GetExistingSystemManaged<Game.UI.NameSystem>();
                if (nameSystem == null) return "";

                var method = typeof(Game.UI.NameSystem).GetMethod(
                    "GetRenderedLabelName",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    return (string)method.Invoke(nameSystem, new object[] { district }) ?? "";
                }

                var method2 = typeof(Game.UI.NameSystem).GetMethod(
                    "GetName",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method2 != null)
                {
                    var res = method2.Invoke(nameSystem, new object[] { district });
                    return res?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"GetDistrictName failed: {ex.Message}");
            }
            return "";
        }

        public static List<(Entity Entity, string Name)> GetAllDistricts(EntityManager em)
        {
            var result = new List<(Entity, string)>();
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<Game.Areas.District>());
            try
            {
                var districts = query.ToEntityArray(Allocator.Temp);
                try
                {
                    foreach (var d in districts)
                        result.Add((d, GetDistrictName(em, d)));
                }
                finally { districts.Dispose(); }
            }
            finally { query.Dispose(); }
            return result;
        }

        public static string GetCurrentCityName()
        {
            try
            {
                var citySystem = World.DefaultGameObjectInjectionWorld
                    ?.GetExistingSystemManaged<Game.City.CityConfigurationSystem>();
                var name = citySystem?.cityName;
                return string.IsNullOrEmpty(name) ? "Unknown" : name;
            }
            catch { return "Unknown"; }
        }

        public static void deleteRecentRoadsNow(int number)
        {
            if (!Mod.IsInGame)
            {
                UITools.ShowError(
                    "You must load a city before starting Auto Historic Timelapse.");
                return;
            }

            string warning =
                "This will destroy roads in your city.\n" +
                "WARNING:\n" +
                "- This DESTROYS your current city, please save before.\n" +
                "- Make sure you also saved a copy (save two times with two different names).\n" +
                "- Using this function for another purpose and with the intent of saving the game " +
                "afterward may lead to corrupted save file, I do not think saving a game that used" +
                " this function is a good idea.\n\n";

            UITools.ShowConfirm("AUTO HISTORIC TIMELAPSE", warning, () => AutoTimelapseSessionSystem.StepTimelapse(number, 0));
        }
        public static int CountRemainingEdges()
        {
            try
            {
                EntityManager? entityManager = GetEntityManager();
                if (entityManager == null) return 0;
                EntityManager em = entityManager.Value;

                string districtFilter = (Mod.Setting?.AutoModDistrictFilter ?? "").Trim();
                var resolvedDistricts = ResolveDistrictsFromFilter(districtFilter);
                var districtEntities = resolvedDistricts.Select(d => d.Entity).ToList();
                bool hasFilter = !string.IsNullOrEmpty(districtFilter);

                if (hasFilter && districtEntities.Count == 0) return 0;

                var query = em.CreateEntityQuery(GetVisibleEdgesQueryDesc());
                try
                {
                    var edges = query.ToEntityArray(Allocator.Temp);
                    try
                    {
                        int count = 0;
                        for (int i = 0; i < edges.Length; i++)
                        {
                            if (IsUnderground(em, edges[i])) continue;        // ← skip souterrain
                            if (hasFilter && GetEdgeFirstDistrictIndex(edges[i], districtEntities) < 0) continue;
                            count++;
                        }
                        return count;
                    }
                    finally { edges.Dispose(); }
                }
                finally { query.Dispose(); }
            }
            catch
            {
                return 0;
            }
        }

    }
}
