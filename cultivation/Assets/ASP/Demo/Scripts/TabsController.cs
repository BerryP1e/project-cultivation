using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ASP.Demo
{
    public class TabsController : MonoBehaviour
    {
        private int m_currentTabIndex = 0;
        private bool m_isAnimating = false;
        [SerializeField] private RectTransform[] Tabs;
        [SerializeField] private RectTransform[] Panels;
        public Action OnTabClick;
        public int CurrentTabIndex => m_currentTabIndex;
        private void Start()
        {
            OnTabClicked(1);
        }

        private float OutElastic(float t)
        {
            float p = 0.3f;
            return (float)Math.Pow(2, -10 * t) * (float)Math.Sin((t - p / 4) * (2 * Math.PI) / p) + 1;
        }

        public float InQuart(float t) => t * t * t * t;
        public float OutQuart(float t) => 1 - InQuart(1 - t);

        private IEnumerator MoveToPosition(int index, Vector2 target)
        {
            var elapsedTime = 0f;
            while (elapsedTime <= 0.35f)
            {
                Panels[index].anchoredPosition =
                    Vector3.Lerp(Panels[index].anchoredPosition, target, InQuart(elapsedTime / 0.35f));
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Panels[index].anchoredPosition = target;

        }

        private IEnumerator PerformPanelShowUp(int index, Action onFinish = null)
        {
            m_isAnimating = true;
            Tabs[index].GetComponent<Image>().color = Color.cyan * 0.5f;
            if (index != m_currentTabIndex)
            {
                Tabs[m_currentTabIndex].GetComponent<Image>().color = Color.white;
                var targetPosition = new Vector2(-365.0f, Panels[m_currentTabIndex].anchoredPosition.y);
                yield return StartCoroutine(MoveToPosition(m_currentTabIndex, targetPosition));
            }

            m_currentTabIndex = index;
            yield return StartCoroutine(MoveToPosition(m_currentTabIndex,
                new Vector2(15, Panels[m_currentTabIndex].anchoredPosition.y)));
            m_isAnimating = false;
            OnTabClick?.Invoke();
        }


        public void OnTabClicked(int index)
        {
            if (!m_isAnimating)
            {
                StartCoroutine(PerformPanelShowUp(index));
            }
        }
    }
}
