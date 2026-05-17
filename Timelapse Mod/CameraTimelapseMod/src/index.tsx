import { ModRegistrar } from "cs2/modding";
import { bindValue, trigger, useValue } from "cs2/api";
import React, { useState } from "react";
import { Button, Panel, Portal } from "cs2/ui";


const MOD_ID = "CameraTimelapseMod";

// ---------- Bindings ----------
//< InfoRow label = "ETA" value = { formatEta(s.etaSeconds) } />

const presetsJson$ = bindValue<string>(MOD_ID, "presetsJson", "{\"Items\":[]}");
const openPanel$ = bindValue<boolean>(MOD_ID, "openPanel", false);
const sessionProgressJson$ = bindValue<string>(MOD_ID, "sessionProgressJson", "{\"active\":false}");
const autoTimelapseJson$ = bindValue<string>(MOD_ID, "autoTimelapseJson", "{\"active\":false}");
const isPhotoModeActive$ = bindValue<boolean>(MOD_ID, "isPhotoModeActive", false);

// ---------- Shared styles ----------

const SEPARATOR_COLOR = "rgba(255,255,255,0.2)";
const SUBTLE_COLOR = "rgba(255,255,255,0.1)";

const sectionStyle: React.CSSProperties = {
    padding: "10rem",
    borderTop: `1px solid ${SEPARATOR_COLOR}`,
};

const sectionFirstStyle: React.CSSProperties = {
    padding: "10rem",
};

const rowStyle: React.CSSProperties = {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    padding: "3rem 0",
    fontSize: "13rem",
};

const rowLabelStyle: React.CSSProperties = {
    opacity: 0.85,
};

const rowValueStyle: React.CSSProperties = {
    fontWeight: 500,
};

const buttonsRowStyle: React.CSSProperties = {
    display: "flex",
    gap: "5rem",
    padding: "10rem",
    borderTop: `1px solid ${SEPARATOR_COLOR}`,
};

const buttonsColumnStyle: React.CSSProperties = {
    display: "flex",
    flexDirection: "column",
    gap: "5rem",
    padding: "10rem",
    borderTop: `1px solid ${SEPARATOR_COLOR}`,
};

const presetRowStyle: React.CSSProperties = {
    display: "flex",
    alignItems: "center",
    padding: "5rem 10rem",
    gap: "5rem",
    borderBottom: `1px solid ${SUBTLE_COLOR}`,
};

const inputStyle: React.CSSProperties = {
    flex: 1,
    background: "rgba(0,0,0,0.4)",
    color: "white",
    border: `1px solid rgba(255,255,255,0.4)`,
    padding: "4rem 8rem",
    borderRadius: "3rem",
    outline: "none",
};

const footerNoteStyle: React.CSSProperties = {
    padding: "10rem",
    opacity: 0.7,
    fontSize: "12rem",
    borderTop: `1px solid ${SEPARATOR_COLOR}`,
};

const warningStyle: React.CSSProperties = {
    color: "#ffaa55",
    marginBottom: "8rem",
};

const commentStyle: React.CSSProperties = {
    fontSize: "12rem",
    opacity: 0.8,
};

// ---------- Inline overrides for variant Button ----------

const primaryBtnOverride: React.CSSProperties = {
    padding: "6rem 14rem",
    fontSize: "13rem",
    textTransform: "none",
    letterSpacing: "normal",
    minHeight: "28rem",
    lineHeight: "1",
    height: "28rem",
};

const flatBtnOverride: React.CSSProperties = {
    padding: "6rem 14rem",
    marginBottom: "2rem",
    marginLeft: "2rem",
    fontSize: "13rem",
    minHeight: "28rem",
    lineHeight: "1",
    height: "28rem",
};
// ---------- Types ----------

interface AutoTimelapseProgress {
    active: boolean;
    paused?: boolean;
    currentStep: number;
    totalSteps: number;
    totalEdgesProcessed: number;
    edgesLeft: number;
    phase: string;
    folder: string;
    comment?: string;
}

interface SessionProgress {
    active: boolean;
    paused?: boolean;
    saveIdx?: number;
    saveTotal?: number;
    viewIdx?: number;
    viewTotal?: number;
    timeIdx?: number;
    timeTotal?: number;
    currentTime?: number;
    currentSave?: string;
    phase?: string;
    etaSeconds?: number;
    completedScreenshots?: number;
    comment?: string;
}

interface CameraPreset {
    Name: string;
    PivotX: number; PivotY: number; PivotZ: number;
    Zoom: number;
    Rotation: { x: number; y: number; z: number };
    HasPhotoMode: boolean;
}

interface CameraPresetList {
    Items: CameraPreset[];
}

// ---------- Helpers ----------

function formatEta(s: number | undefined): string {
    if (s === undefined || s < 0) return "calculating...";
    if (s < 60) return `${s}s`;
    const m = Math.floor(s / 60);
    const sec = s % 60;
    if (m < 60) return `${m}m ${sec}s`;
    const h = Math.floor(m / 60);
    const mm = m % 60;
    return `${h}h ${mm}m`;
}

function formatTime(t: number | undefined): string {
    if (t === undefined) return "";
    const h = Math.floor(t);
    const m = Math.floor((t - h) * 60);
    return m > 0 ? `${h}h${m.toString().padStart(2, "0")}` : `${h}h`;
}

// ---------- Reusable Row component ----------

const InfoRow = ({ label, value }: { label: string; value: React.ReactNode }) => (
    <div style={rowStyle}>
        <span style={rowLabelStyle}>{label}</span>
        <span style={rowValueStyle}>{value}</span>
    </div>
);

// ---------- AutoTimelapsePanel ----------

const AutoTimelapsePanel = () => {
    const json = useValue(autoTimelapseJson$);

    let s: AutoTimelapseProgress = {
        active: false, currentStep: 0, totalSteps: 0,
        totalEdgesProcessed: 0, edgesLeft: 0, phase: "", folder: ""
    };
    try { s = JSON.parse(json); }
    catch (e) { console.warn("AutoTimelapseProgress parse failed:", e, json); }

    if (!s.active) return null;

    return (
        <Portal>
            <Panel
                header={<>{s.paused ? "Auto Historic Timelapse — Paused" : "Auto Historic Timelapse"}</>}
                style={{
                    position: "absolute",
                    top: "50rem",
                    left: "10rem",
                    width: "420rem",
                }}
            >
                <div style={sectionFirstStyle}>
                    <div style={warningStyle}>⚠ Do NOT save this game</div>

                    <InfoRow label="Step" value={`${s.currentStep} / ~${s.totalSteps}`} />
                    <InfoRow label="Edges destroyed" value={s.totalEdgesProcessed} />
                    <InfoRow label="Edges left" value={s.edgesLeft} />
                    <InfoRow label="Phase" value={s.phase} />
                </div>

                {s.comment && (
                    <div style={sectionStyle}>
                        <div style={commentStyle}>{s.comment}</div>
                    </div>
                )}

                <div style={buttonsRowStyle}>
                    {s.paused ? (
                        <Button variant="primary" style={primaryBtnOverride} onSelect={() => trigger(MOD_ID, "autoTimelapseResume")}>
                            Resume [numpad 0]
                        </Button>
                    ) : (
                        <Button variant="primary" style={primaryBtnOverride} onSelect={() => trigger(MOD_ID, "autoTimelapsePause")}>
                            Pause [numpad 0]
                        </Button>
                    )}
                    <Button variant="flat" style={flatBtnOverride} onSelect={() => trigger(MOD_ID, "autoTimelapseStop")}>
                        Stop [numpad Enter]
                    </Button>
                </div>
            </Panel>
        </Portal>
    );
};

// ---------- SessionProgressPanel ----------

const SessionProgressPanel = () => {
    const json = useValue(sessionProgressJson$);

    let s: SessionProgress = { active: false };
    try { s = JSON.parse(json); }
    catch (e) { console.warn("SessionProgress parse failed:", e, json); }

    if (!s.active) return null;

    return (
        <Portal>
            <Panel
                header={<>{s.paused ? "Paused" : "Capturing..."}</>}
                style={{
                    position: "absolute",
                    top: "50rem",
                    left: "10rem",
                    width: "400rem",
                }}
            >
                <div style={sectionFirstStyle}>
                    <InfoRow
                        label="Save"
                        value={`${s.saveIdx}/${s.saveTotal}${s.currentSave ? ` (${s.currentSave})` : ""}`}
                    />
                    <InfoRow label="View" value={`${s.viewIdx}/${s.viewTotal}`} />
                    <InfoRow
                        label="Time"
                        value={`${s.timeIdx}/${s.timeTotal} (${formatTime(s.currentTime)})`}
                    />
                    <InfoRow label="Phase" value={s.phase ?? ""} />
                </div>

                <div style={sectionStyle}>
                    <InfoRow label="Captured" value={s.completedScreenshots ?? 0} />
                </div>

                {s.comment && (
                    <div style={sectionStyle}>
                        <div style={commentStyle}>{s.comment}</div>
                    </div>
                )}

                <div style={buttonsRowStyle}>
                    {s.paused ? (
                        <Button variant="primary" style={primaryBtnOverride} onSelect={() => trigger(MOD_ID, "sessionResume")}>
                            Resume [numpad 0]
                        </Button>
                    ) : (
                        <Button variant="primary" style={primaryBtnOverride} onSelect={() => trigger(MOD_ID, "sessionPause")}>
                            Pause [numpad 0]
                        </Button>
                    )}
                    <Button variant="flat" style={flatBtnOverride} onSelect={() => trigger(MOD_ID, "sessionStop")}>
                        Stop [numpad enter]
                    </Button>
                </div>

                <div style={buttonsColumnStyle}>
                    <Button variant="flat" style={flatBtnOverride} onSelect={() => trigger(MOD_ID, "openScreenshotFolder")}>
                        Open screenshot folder
                    </Button>
                    <Button variant="flat" style={flatBtnOverride} onSelect={() => trigger(MOD_ID, "openForumTopic")}>
                        Open forum topic
                    </Button>
                    <Button variant="flat" style={flatBtnOverride} onSelect={() => trigger(MOD_ID, "writeEmail")}>
                        Send an email
                    </Button>
                </div>
            </Panel>
        </Portal>
    );
};

// ---------- PresetManagerPanel ----------

const PresetManagerPanel = () => {
    const [isOpen, setIsOpen] = useState(false);
    const [editingIndex, setEditingIndex] = useState<number | null>(null);
    const [editValue, setEditValue] = useState("");
    const requestedOpen = useValue(openPanel$);
    const photoModeActive = useValue(isPhotoModeActive$);

    React.useEffect(() => {
        setIsOpen(requestedOpen);
    }, [requestedOpen]);

    const json = useValue(presetsJson$);
    let list: CameraPresetList = { Items: [] };
    try { list = JSON.parse(json); }
    catch (e) { console.warn("PresetsJson parse failed:", e, json); }

    const presets = list.Items || [];

    const startEditing = (i: number, currentName: string) => {
        setEditingIndex(i);
        setEditValue(currentName || `Preset ${i + 1}`);
    };

    const commitEdit = () => {
        if (editingIndex !== null && editValue.trim().length > 0) {
            trigger(MOD_ID, "renamePreset", editingIndex, editValue.trim());
        }
        setEditingIndex(null);
        setEditValue("");
    };

    const cancelEdit = () => {
        setEditingIndex(null);
        setEditValue("");
    };

    if (!isOpen) return null;

    return (
        <Portal>
            <Panel
                header={<>Camera Presets ({presets.length})</>}
                onClose={() => {
                    setIsOpen(false);
                    trigger(MOD_ID, "panelClosed");
                }}
                style={{
                    position: "absolute",
                    top: "150rem",
                    left: "10rem",
                    width: "400rem",
                    maxHeight: "600rem",
                }}
            >
                {presets.length === 0 && (
                    <div style={{ padding: "10rem" }}>
                        No presets yet. Position your camera and use the Add button below.
                    </div>
                )}

                {presets.map((p, i) => (
                    <div key={i} style={presetRowStyle}>
                        {editingIndex === i ? (
                            <input
                                type="text"
                                value={editValue}
                                onChange={(e) => setEditValue(e.target.value)}
                                onBlur={commitEdit}
                                onKeyDown={(e) => {
                                    if (e.key === "Enter") commitEdit();
                                    else if (e.key === "Escape") cancelEdit();
                                }}
                                autoFocus
                                style={inputStyle}
                            />
                        ) : (
                            <div
                                style={{
                                    flex: 1,
                                    cursor: "pointer",
                                    display: "flex",
                                    alignItems: "center",
                                    gap: "5rem",
                                }}
                                onDoubleClick={() => startEditing(i, p.Name)}
                                title={p.HasPhotoMode
                                    ? "Double-click to rename. Photo mode settings are saved with this preset."
                                    : "Double-click to rename"}
                            >
                                <span>{p.Name || `Preset ${i + 1}`}</span>
                                {p.HasPhotoMode && (
                                    <span
                                        style={{ fontSize: "14rem", color: "#88ccff", paddingLeft: "2rem" }}
                                        title="Photo mode settings saved with this preset"
                                    >
                                        [with camera mode]
                                    </span>
                                )}
                            </div>
                        )}
                        <Button
                            variant="flat"
                            style={{ ...flatBtnOverride, marginRight: "10rem" }}
                            onSelect={() => trigger(MOD_ID, "gotoPreset", i)}
                        >
                            See
                        </Button>
                        <Button
                            variant="flat"
                            style={flatBtnOverride}
                            onSelect={() => trigger(MOD_ID, "deletePreset", i)}
                        >
                            X
                        </Button>
                    </div>
                ))}

                <div style={buttonsRowStyle}>
                    <Button
                        variant="primary"
                        style={{ ...primaryBtnOverride, marginRight: "10rem" }}
                        onSelect={() => trigger(MOD_ID, "captureCurrentAsPreset")}
                    >
                        + Add current view
                    </Button>
                    <Button
                        variant="flat"
                        style={flatBtnOverride}
                        onSelect={() => trigger(MOD_ID, "deleteAllPresets")}
                    >
                        Delete all
                    </Button>
                </div>

                <div style={buttonsRowStyle}>
                    <Button
                        variant="flat"
                        style={{ ...flatBtnOverride, marginRight: "10rem" }}
                        onSelect={() => trigger(MOD_ID, "exportPresets")}
                    >
                        Export presets
                    </Button>
                    <Button
                        variant="flat"
                        style={flatBtnOverride}
                        onSelect={() => trigger(MOD_ID, "importPresets")}
                    >
                        Import presets
                    </Button>
                </div>

                {photoModeActive && (
                    <div style={buttonsRowStyle}>
                        <Button variant="flat" style={flatBtnOverride} onSelect={() => trigger(MOD_ID, "exitPhotoMode")}>
                            Exit camera mode
                        </Button>
                        <span
                            style={{
                                flex: 1,
                                fontSize: "11rem",
                                marginRight: "2rem",
                                marginLeft: "4rem",
                                opacity: 0.7,
                                alignSelf: "center",
                            }}
                        >
                            Photo mode is currently active
                        </span>
                    </div>
                )}

                <div style={footerNoteStyle}>
                    Double-click a preset name to rename it.<br />
                    To save Camera Mode settings in a preset, open camera Mode.<br />
                    More options in Game Options → Auto TimeLapse Mod.
                </div>
            </Panel>
        </Portal>
    );
};

// ---------- Register ----------

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("Game", PresetManagerPanel);
    moduleRegistry.append("Game", SessionProgressPanel);
    moduleRegistry.append("Game", AutoTimelapsePanel);
    console.log("CameraTimelapseMod UI registered");
};

export default register;