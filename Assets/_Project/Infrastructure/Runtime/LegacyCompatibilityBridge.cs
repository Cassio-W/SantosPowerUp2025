using System;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Ponte de compatibilidade temporária com sistemas legados (Canvas, UIManager, CameraFocusManager, AttributeCameraEffects).
    /// Isola todas as buscas globais e reflexões para eliminação definitiva na Fase 5.
    /// </summary>
    [Obsolete("Componente temporário de compatibilidade. Será removido na Fase 5.")]
    public class LegacyCompatibilityBridge
    {
        public void SuppressLegacyCanvas(bool disableLegacyCanvas)
        {
            if (!disableLegacyCanvas) return;

            try
            {
                var canvasObjects = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in canvasObjects)
                {
                    if (c == null) continue;
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                    {
                        var transforms = c.GetComponentsInChildren<Transform>(true);
                        foreach (var t in transforms)
                        {
                            if (t == null || t == c.transform) continue;
                            string lower = t.name.ToLower();
                            if (lower.Contains("dealpanel") || lower.Contains("gameover") || lower.Contains("decisionbutton") || lower.Contains("corruptionpanel"))
                            {
                                t.gameObject.SetActive(false);
                            }
                        }
                    }
                }

                var legacyUIManagers = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mb in legacyUIManagers)
                {
                    if (mb != null && mb.GetType().Name == "UIManager")
                    {
                        mb.enabled = false;
                    }
                }
            }
            catch { }
        }

        public void NotifyCameraUnfocus()
        {
            try
            {
                Type focusType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    focusType = asm.GetType("CameraFocusManager");
                    if (focusType != null) break;
                }

                if (focusType != null)
                {
                    var instanceProp = focusType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instanceProp?.GetValue(null) ?? UnityEngine.Object.FindFirstObjectByType(focusType);
                    if (inst != null)
                    {
                        var hasFocusProp = focusType.GetProperty("HasActiveFocus");
                        bool hasFocus = (bool)(hasFocusProp?.GetValue(inst) ?? false);
                        if (hasFocus)
                        {
                            focusType.GetMethod("Unfocus")?.Invoke(inst, null);
                        }
                    }
                }
            }
            catch { }
        }

        public void SyncCameraEffects(StatBlock stats, bool instant = false)
        {
            if (stats == null) return;
            try
            {
                Type fxType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    fxType = asm.GetType("AttributeCameraEffects");
                    if (fxType != null) break;
                }

                if (fxType != null)
                {
                    var instanceProp = fxType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instanceProp?.GetValue(null) ?? UnityEngine.Object.FindFirstObjectByType(fxType);
                    if (inst != null)
                    {
                        Type attrType = null;
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            attrType = asm.GetType("Attributes");
                            if (attrType != null) break;
                        }

                        if (attrType != null)
                        {
                            object attrInst = Activator.CreateInstance(attrType);
                            attrType.GetField("climaticChanges")?.SetValue(attrInst, stats.climaticChanges);
                            attrType.GetField("corruption")?.SetValue(attrInst, stats.corruption);
                            attrType.GetField("economy")?.SetValue(attrInst, stats.economy);
                            attrType.GetField("internationalRelations")?.SetValue(attrInst, stats.internationalRelations);
                            attrType.GetField("populationalApproval")?.SetValue(attrInst, stats.popularApproval);

                            var applyMethod = fxType.GetMethod("ApplyAttributeEffects", new Type[] { attrType, typeof(bool) });
                            if (applyMethod != null)
                            {
                                applyMethod.Invoke(inst, new object[] { attrInst, instant });
                                return;
                            }
                        }

                        // Fallback direto nos métodos individuais de poluição e corrupção
                        var pollMethod = fxType.GetMethod("UpdatePollutionEffect", new Type[] { typeof(float), typeof(bool) });
                        pollMethod?.Invoke(inst, new object[] { (float)stats.climaticChanges, instant });

                        var corrMethod = fxType.GetMethod("UpdateCorruptionEffect", new Type[] { typeof(float), typeof(bool) });
                        corrMethod?.Invoke(inst, new object[] { (float)stats.corruption, instant });
                    }
                }
            }
            catch { }
        }

        public void SyncCanvasUI(StatBlock stats, string displayDate)
        {
            if (stats == null) return;
            try
            {
                var canvasObjects = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in canvasObjects)
                {
                    if (c == null) continue;
                    var dateObj = c.transform.Find("DateText") ?? c.transform.Find("Header/DateText");
                    if (dateObj != null)
                    {
                        var textComp = dateObj.GetComponent<UnityEngine.UI.Text>();
                        if (textComp != null && !string.IsNullOrEmpty(displayDate))
                        {
                            textComp.text = displayDate;
                        }
                    }
                }
            }
            catch { }
        }
    }
}
