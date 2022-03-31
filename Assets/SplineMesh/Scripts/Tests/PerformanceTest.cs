using System.Collections;
using Unity.PerformanceTesting;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SplineMesh.Scripts.Tests {
    public class PerformanceTest {
        [UnityTest, Performance]
        public IEnumerator Test() {
            SceneManager.LoadScene("Performance Test Scene", LoadSceneMode.Additive);
            yield return Measure.Frames()
                .WarmupCount(30)
                .MeasurementCount(30)
                .Run();
        }

    }
}
