using UnityEngine;

namespace ProjectII
{
    public class HomeRandomPosSelector : MonoBehaviour
    {
        [SerializeField] private GameObject[] posRoots;

        private void OnEnable()
        {
            for (int i = 0; i < posRoots.Length; i++)
            {
                posRoots[i].SetActive(false);
            }

            posRoots[Random.Range(0, posRoots.Length)].SetActive(true);
        }
    }
}
