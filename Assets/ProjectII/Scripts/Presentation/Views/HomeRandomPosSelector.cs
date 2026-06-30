using KahaGameCore.GameEvent;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Events;
using UnityEngine;

namespace ProjectII
{
    public class HomeRandomPosSelector : MonoBehaviour
    {
        [SerializeField] private GameObject[] posRoots;

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
    }
}
