using KahaGameCore.GameEvent;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Events;
using ProjectII.Gameplay.Presentation.Events;
using ProjectII.Gameplay.Presentation.Views;
using UnityEngine;

namespace ProjectII
{
    public class HomeRandomPosSelector : MonoBehaviour
    {
        [SerializeField] private GameObject[] posRoots;
        [SerializeField] private Vector2[] offset;
        [SerializeField] private SubActionMenuView subActionMenuView;

        private void OnEnable()
        {
            EventBus.Subscribe<TimePhaseChangedEvent>(TimePhaseChangedEvent);
            TimePhaseChangedEvent(null);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TimePhaseChangedEvent>(TimePhaseChangedEvent);
        }

        private void TimePhaseChangedEvent(TimePhaseChangedEvent timePhaseChangedEvent)
        {
            for (int i = 0; i < posRoots.Length; i++)
            {
                posRoots[i].SetActive(false);
            }

            posRoots[Random.Range(0, posRoots.Length)].SetActive(true);
        }

        public void Button_CallActionMenu()
        {
            Vector2 pos = Vector2.zero;

            for (int i = 0; i < posRoots.Length; i++)
            {
                if (posRoots[i].activeSelf)
                {
                    pos = posRoots[i].GetComponent<RectTransform>().anchoredPosition + offset[i];
                    break;
                }
            }

            EventBus.Publish(new OpenSubActionMenuRequestedEvent(subActionMenuView, "CharacterAction", pos));
        }

    }
}
