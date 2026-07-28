using UnityEngine;
using UnityEditor;

public static class TestMapGen
{
    public static void Run()
    {
        CreateMap cm = new CreateMap();
        cm.GenerateMap();
        if (cm.lastValidationPassed) {
            Debug.Log("TEST_MAP_GEN: PASSED");
        } else {
            Debug.LogError("TEST_MAP_GEN: FAILED with errors: " + string.Join(", ", cm.lastValidationErrors));
        }
    }
}
