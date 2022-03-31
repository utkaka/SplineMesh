using UnityEngine;
using Random = UnityEngine.Random;

namespace SplineMesh.Scripts.Tests {
    public class TestSceneSpawner : MonoBehaviour {
        [SerializeField]
        private GameObject _prefab;
        [SerializeField]
        private int _objectsCount;
        public void Start() {
            for (var i = 0; i < _objectsCount; i++) {
                var position = Random.insideUnitSphere * 100.0f;
                position.y = 0.0f;
                Instantiate(_prefab, position, Quaternion.identity);
            }
        }
    }
}
