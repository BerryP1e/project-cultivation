using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Object;

/// <summary>编辑器中的真实 Overlay 录制及动效验收；临时状态退出 Play 即丢弃。</summary>
public class InkUIMotionQa
{
    static string status = "idle";
    static bool running;
    public static string Run(string command)
    {
        if (command == "status") return status;
        if (!Application.isPlaying) return "FAIL requires Play";
        if (running) return "FAIL motion QA already running";
        var host = new InkUIMotionQa();
        FindObjectOfType<CharacterPanelUI>(true).StartCoroutine(host.Record()); return "OK motion recording started";
    }
    IEnumerator Record()
    {
        running = true;
        status = "recording";
        string directory = Path.Combine(Application.dataPath, "../screenshots"); Directory.CreateDirectory(directory);
        string temp = Path.Combine(Path.GetTempPath(), "inkui-motion-" + Guid.NewGuid().ToString("N") + ".mp4");
        string final = Path.Combine(directory, "InkUI-motion-" + Screen.width + "x" + Screen.height + "-slow5.mp4");
        var log = new StringBuilder(); int failures = 0;
        Action<string, bool> check = (label, ok) => { log.AppendLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failures++; };
        var role = FindObjectOfType<CharacterPanelUI>(true); var data = role.GetComponent<UIPanelData>();
        var saved = new List<UnityEngine.Object>(data.主动技能);
        var vitals = FindObjectOfType<PlayerVitals>(); float health = vitals.当前气血;
        bool reduced = UIInkMotion.减少动效; UIInkMotion.减少动效 = false;
        int fps = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
        Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        MediaEncoder encoder = null; Texture2D frame = null;
        UIEntryRow card = null; UIActiveSkillBar bar = null; Vector3 iconPosition = Vector3.zero;
        var window = role.transform.Find("Window") as RectTransform;
        if (window == null) window = role.panelRoot.GetComponentInChildren<UIInkMotion>(true)?.transform as RectTransform;
        Vector2 panelPosition = window != null ? window.anchoredPosition : Vector2.zero;
        try
        {
            role.SetOpen(false); yield return new WaitForSecondsRealtime(.4f);
            encoder = new MediaEncoder(temp, new VideoTrackAttributes { frameRate = new MediaRational(5), width = (uint)Screen.width, height = (uint)Screen.height, includeAlpha = false, bitRateMode = VideoBitrateMode.Medium });
            float started = Time.realtimeSinceStartup;
            for (int index = 0; index < 240; index++)
            {
                if (index == 10) role.SetOpen(true);
                if (index == 13)
                {
                    var panelMotion = window != null ? window.GetComponent<UIInkMotion>() : null;
                    check("panel motion attached", panelMotion != null);
                    check("panel position unchanged during reveal", window != null && window.anchoredPosition == panelPosition);
                    check("panel has horizontal reveal", window != null && window.GetComponent<UIInkReveal>() != null);
                }
                if (index == 35) role.ShowTabByIndex(2);
                if (index == 55)
                {
                    check("panel reveal completes without budget deadlock", window.GetComponent<UIInkReveal>().进度 == 1 && window.GetComponent<CanvasGroup>().alpha == 1);
                    foreach (var row in role.GetComponentsInChildren<UIEntryRow>())
                        if (row.Owner != null && row.Owner.inkCards && row.Entry is ActiveDivineAbility) { card = row; break; }
                    bar = role.GetComponentInChildren<UIActiveSkillBar>();
                    iconPosition = card.swatch.transform.localPosition;
                    ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                }
                if (index == 65)
                {
                    check("card icon floats and text keeps scale", card.swatch.transform.localPosition.y > iconPosition.y && card.label.transform.localScale == Vector3.one);
                    ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
                }
                if (index == 69) ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);
                if (index == 76) ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                if (index == 85)
                {
                    var e = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, card.transform.position) };
                    card.OnBeginDrag(e);
                }
                if (index >= 86 && index <= 100)
                {
                    Vector2 a = RectTransformUtility.WorldToScreenPoint(null, card.transform.position);
                    Vector2 b = RectTransformUtility.WorldToScreenPoint(null, bar.slots[5].transform.position);
                    card.OnDrag(new PointerEventData(EventSystem.current) { position = Vector2.Lerp(a, b, (index - 85) / 15f) });
                }
                if (index == 101)
                {
                    bar.slots[5].OnDrop(new PointerEventData(EventSystem.current));
                    check("real drop equips shared slot and clears context", data.主动技能[5] == card.Entry as UnityEngine.Object && !UIDragContext.Dragging);
                }
                if (index == 115) vitals.当前气血 = Mathf.Max(1, health * .6f);
                if (index == 119)
                {
                    var number = FindObjectOfType<PlayerHud>().气血文字;
                    check("HUD number animator attached", number.GetComponent<UIInkNumber>() != null);
                    var first = System.Text.RegularExpressions.Regex.Match(number.text, @"\d+");
                    float displayed = first.Success ? float.Parse(first.Value) : -1;
                    check("HUD number has intermediate display without changing data", displayed > Mathf.Round(vitals.当前气血) && displayed < health);
                }
                if (index == 135) vitals.当前气血 = health;
                if (index == 150) ToastUI.提示("材料不足", .5f);
                if (index == 185)
                {
                    role.SetOpen(false);
                    check("close immediately hides business root", !role.IsOpen && !role.panelRoot.activeSelf);
                    check("close immediately restores time", Time.timeScale > 0);
                }
                yield return new WaitForEndOfFrame();
                frame = ScreenCapture.CaptureScreenshotAsTexture();
                if (index == 13 || index == 65 || index == 100 || index == 119 || index == 157 || index == 190)
                    File.WriteAllBytes(Path.Combine(directory, "InkUI-motion-frame-" + index + "-" + Screen.width + "x" + Screen.height + ".png"), frame.EncodeToPNG());
                if (!encoder.AddFrame(frame)) throw new InvalidOperationException("MediaEncoder rejected frame " + index);
                Destroy(frame); frame = null;
            }
            float elapsed = Time.realtimeSinceStartup - started;
            encoder.Dispose(); encoder = null;
            File.Copy(temp, final, true); File.Delete(temp);
            check("motion budget peak at most six", UIInkMotion.峰值 <= 6);
            check("close visual cleaned up", FindObjectsOfType<UIInkCloseVisual>().Length == 0);
            check("drag context cleared", !UIDragContext.Dragging);
            role.SetOpen(true);
            for (int i = 0; i < 24; i++) { role.ShowTabByIndex(i % 8); yield return null; }
            role.SetOpen(false); yield return new WaitForSecondsRealtime(.6f);
            check("rapid tab switches release all motion reservations", UIInkMotion.运动数 == 0);
            // 真正点击可访问开关，随后恢复用户原偏好。
            InkUIQa.Panel("pause"); yield return new WaitForSecondsRealtime(.3f);
            Button reduceButton = null;
            foreach (var button in FindObjectsOfType<Button>()) if (button.name == "减少动效") { reduceButton = button; break; }
            check("reduced motion control is available", reduceButton != null);
            if (reduceButton != null) reduceButton.onClick.Invoke();
            check("reduced motion toggle persists preference", UIInkMotion.减少动效);
            InkUIQa.Panel("close"); role.SetOpen(true); role.ShowTabByIndex(2);
            yield return new WaitForSecondsRealtime(.35f);
            check("reduced panel is fully visible", window.GetComponent<CanvasGroup>().alpha == 1 && window.GetComponent<UIInkReveal>().进度 == 1);
            role.SetOpen(false); yield return new WaitForSecondsRealtime(.25f);
            check("reduced close creates no visual ghost", FindObjectsOfType<UIInkCloseVisual>().Length == 0);
            log.AppendLine("Frames=240 EncodeFps=5 CaptureSeconds=" + elapsed.ToString("F2") + " AverageCaptureFps=" + (240 / elapsed).ToString("F2"));
            log.AppendLine("Peak=" + UIInkMotion.峰值 + " Failures=" + failures + " Video=" + final);
            status = "DONE Failures=" + failures + " Video=" + final;
        }
        finally
        {
            encoder?.Dispose(); if (frame != null) Destroy(frame);
            vitals.当前气血 = health; UIInkMotion.减少动效 = reduced;
            Application.targetFrameRate = fps; QualitySettings.vSyncCount = vsync;
            UIDragContext.End(true);
            for (int i = 0; i < saved.Count; i++) { if (saved[i] == null) data.ClearSlot(i); else data.EquipToSlot(i, saved[i]); }
            File.WriteAllText(Path.Combine(directory, "InkUI-motion-checks-" + Screen.width + "x" + Screen.height + ".txt"), log.ToString());
            if (status == "recording") status = "FAILED recording aborted; see Console and motion-checks log";
            running = false;
        }
    }
}
