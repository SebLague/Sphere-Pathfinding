using UnityEditor;
using UnityEngine;

[CustomEditor (typeof (Pathfinder))]
public class DemoEditor : Editor {

    void OnSceneGUI () {
        var pathfinder = (Pathfinder) target;

        if (Application.isPlaying && pathfinder.showTestPath) {
            var testPath = pathfinder.FindPath (pathfinder.testA.position, pathfinder.testB.position);
            if (testPath != null) {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                Handles.color = Color.white;
                for (int i = 0; i < testPath.Count; i++) {
                    Handles.SphereHandleCap (0, testPath[i], Quaternion.identity, .2f, EventType.Repaint);
                }

            }

            if (Event.current.shift && !Event.current.alt) {
                Vector2 mouse = Event.current.mousePosition;
                mouse = new Vector2 (mouse.x, Camera.current.pixelHeight - mouse.y);
                Ray ray = Camera.current.ScreenPointToRay (mouse);
                RaycastHit hit;
                if (Physics.Raycast (ray, out hit)) {
                    var t = pathfinder.testA;
                    t.position = hit.point;
                    t.up = t.position.normalized;
                }
            }
        }
    }
}