using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>只插值显示数字，不写回数据。倒计时、日期、槽位编号不挂它。</summary>
[DefaultExecutionOrder(10000)]
public class UIInkNumber : MonoBehaviour
{
    static readonly Regex Digits = new Regex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
    Text label;
    string desired, written;
    double[] from, to;
    float elapsed;
    bool ownsBudget;
    public static void Attach(Text text)
    {
        if (text != null && InkUITheme.Enabled && text.GetComponent<UIInkNumber>() == null) text.gameObject.AddComponent<UIInkNumber>();
    }
    void Awake() { label = GetComponent<Text>(); }
    void OnDisable()
    {
        if (label != null && desired != null) label.text = desired;
        if (ownsBudget) { UIInkMotion.Release(); ownsBudget = false; }
        written = desired = null;
    }
    void LateUpdate()
    {
        if (label == null) return;
        string raw = label.text;
        if (desired == null) { desired = written = raw; return; }
        if (raw != written && raw != desired)
        {
            var previous = Digits.Matches(written ?? raw); var next = Digits.Matches(raw);
            desired = raw; elapsed = 0;
            if (previous.Count == 0 || previous.Count != next.Count || Digits.Replace(written ?? raw, "#") != Digits.Replace(raw, "#"))
            { Finish(); return; }
            from = new double[next.Count]; to = new double[next.Count];
            for (int i = 0; i < next.Count; i++)
            {
                double.TryParse(previous[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out from[i]);
                double.TryParse(next[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out to[i]);
            }
            if (!ownsBudget) ownsBudget = UIInkMotion.Acquire();
        }
        if (!ownsBudget || UIInkMotion.减少动效) { Finish(); return; }
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= UIInkMotion.Timing.Number) { Finish(); return; }
        int index = 0; float t = UIInkMotion.Timing.InOut(elapsed / UIInkMotion.Timing.Number);
        written = Digits.Replace(desired, match => {
            int i = index++; double number = from[i] + (to[i] - from[i]) * t;
            int decimals = match.Value.Contains(".") ? match.Value.Length - match.Value.IndexOf('.') - 1 : 0;
            return number.ToString("F" + decimals, CultureInfo.InvariantCulture);
        });
        label.text = written;
    }
    void Finish()
    {
        label.text = written = desired;
        if (ownsBudget) { UIInkMotion.Release(); ownsBudget = false; }
    }
}
