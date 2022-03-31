using System.Collections;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

namespace SplineMesh.Scripts.Tests {
    public class PerformanceTestPrebuildStep : IPrebuildSetup {
        public void Setup() {
            for (var i = 0; i < 50; i++) {
                var testPrefab = Resources.Load("GrowingRoot");
                var position = Random.insideUnitSphere * 100.0f;
                position.y = 0.0f;
                Object.Instantiate(testPrefab, position, Quaternion.identity);
            }
        }
    }
    
    public class PerformanceTest {
        [UnityTest, Performance, PrebuildSetup(typeof(PerformanceTestPrebuildStep))]
        public IEnumerator Test() {
            yield return Measure.Frames()
                .WarmupCount(30)
                .MeasurementCount(30)
                .Run();
        }

    }
}
