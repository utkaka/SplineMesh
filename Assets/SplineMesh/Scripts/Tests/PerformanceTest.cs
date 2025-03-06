using System.Collections;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SplineMesh.Scripts.Tests {
    public class PerformanceTest {
        public class TestCase {
            private string _prefab;
            private int _count;

            public string Prefab => _prefab;
            public int Count => _count;

            public TestCase(string prefab, int count) {
                _prefab = prefab;
                _count = count;
            }
            
            public override string ToString() {
                return $"{_prefab}: {_count}";
            }
        }
        
        private static IEnumerable TestCases() {
            yield return new TestCase("Highpoly root", 1);
            yield return new TestCase("Lowpoly root", 50);
        }

        [UnityTest, Performance]
        public IEnumerator Test([ValueSource(nameof(TestCases))]TestCase testCase) {
            
            var sampleGroups = new []{
                new SampleGroup("PlayerLoop", SampleUnit.Microsecond),
#if UNITY_STANDALONE_OSX
                new SampleGroup("GfxDeviceMetal.WaitForLastPresent", SampleUnit.Microsecond)
#else
				new SampleGroup("Gfx.WaitForPresentOnGfxThread", SampleUnit.Microsecond)
#endif
            };

            Application.targetFrameRate = 5000;
			
            var cameraTransform = new GameObject("Main Camera").transform;
            cameraTransform.gameObject.tag = "MainCamera";
            cameraTransform.position = new Vector3(0.38f, 1.17f, -1.44f);
            cameraTransform.rotation = Quaternion.Euler(32.06f, -12.81f, 0.0f);
            
            var prefab = Resources.Load<GameObject>(testCase.Prefab);
            
            var root = new GameObject("Root").transform;
            
            for (var i = 0; i < testCase.Count; i++) {
                var position = Random.insideUnitSphere * 100.0f;
                position.y = 0.0f;
                Object.Instantiate(prefab, position, Quaternion.identity, root);
            }
            yield return Measure.Frames()
                .WarmupCount(60)
                .ProfilerMarkers(sampleGroups)
                .MeasurementCount(60)
                .Run();
            Object.Destroy(root.gameObject);
            Object.Destroy(cameraTransform.gameObject);
        }
    }
}
