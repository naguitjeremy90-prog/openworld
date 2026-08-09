using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace TargetIndicators
{
    [CustomEditor(typeof(TargetIndicatorManager))]
    public class TargetIndicatorManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.Update();

            var scriptProperty = serializedObject.FindProperty("m_Script");
            GUI.enabled = false;
            EditorGUILayout.PropertyField(scriptProperty, true);
            GUI.enabled = true;

            var camera = serializedObject.FindProperty("_camera");
            var boundaryType = serializedObject.FindProperty("_boundaryType");
            var boundaryShape = serializedObject.FindProperty("_boundaryShape");

            var topPadding = serializedObject.FindProperty("_topPadding");
            var bottomPadding = serializedObject.FindProperty("_bottomPadding");
            var leftPadding = serializedObject.FindProperty("_leftPadding");
            var rightPadding = serializedObject.FindProperty("_rightPadding");

            var width = serializedObject.FindProperty("_width");
            var height = serializedObject.FindProperty("_height");

            EditorGUILayout.PropertyField(camera, new GUIContent("Camera"));
            EditorGUILayout.PropertyField(boundaryType, new GUIContent("Boundary Type"));

            if (boundaryShape != null)
            {
                if (boundaryShape.enumValueIndex < 0)
                    boundaryShape.enumValueIndex = (int)BoundaryShape.Rectangle;

                if (boundaryType.enumValueIndex is (int)BoundaryType.Padded or (int)BoundaryType.Absolute)
                {
                    EditorGUILayout.PropertyField(boundaryShape, new GUIContent("Boundary Shape"));
                }
            }

            if (boundaryType != null && boundaryShape != null)
            {
                switch (boundaryType.enumValueIndex)
                {
                    case (int)BoundaryType.Padded when
                        boundaryShape.enumValueIndex is (int)BoundaryShape.Rectangle or (int)BoundaryShape.Ellipse:
                        EditorGUILayout.PropertyField(topPadding, new GUIContent("Top Padding"));
                        EditorGUILayout.PropertyField(bottomPadding, new GUIContent("Bottom Padding"));
                        EditorGUILayout.PropertyField(leftPadding, new GUIContent("Left Padding"));
                        EditorGUILayout.PropertyField(rightPadding, new GUIContent("Right Padding"));
                        break;
                    case (int)BoundaryType.Absolute:
                        EditorGUILayout.PropertyField(width, new GUIContent("Width"));
                        EditorGUILayout.PropertyField(height, new GUIContent("Height"));
                        break;
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Boundary Visualization Instructions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"To visualize the target indicator boundary add a `{nameof(TargetIndicatorBoundaryVisualizer)}` " +
                $"component to this GameObject",
                MessageType.Info);

            var targetIndicatorManager = (TargetIndicatorManager)target;
            // TODO: dont do getcomponent. look up reference via reflection to ensure not deleting the wrong line renderer
            // if the user already has a line renderer on the component
            var debugLinesComponent = targetIndicatorManager.gameObject.GetComponent<TargetIndicatorBoundaryVisualizer>();
            var hadDebugLines = debugLinesComponent != null;

            if (!hadDebugLines)
            {
                if (GUILayout.Button("Add Boundary Visualizer", GUILayout.Height(30)))
                {
                    Undo.AddComponent<TargetIndicatorBoundaryVisualizer>(targetIndicatorManager.gameObject);
                    EditorUtility.SetDirty(targetIndicatorManager.gameObject);
                }
            }
            else
            {
                if (GUILayout.Button("Remove Boundary Visualizer", GUILayout.Height(30)))
                {
                    var type = typeof(TargetIndicatorBoundaryVisualizer);
                    var fieldInfo = type.GetField("_lineRenderer", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo != null)
                    {
                        var lineRenderer = (LineRenderer)fieldInfo.GetValue(debugLinesComponent);
                        Undo.DestroyObjectImmediate(debugLinesComponent);
                        Undo.DestroyObjectImmediate(lineRenderer);
                    }

                    EditorUtility.SetDirty(targetIndicatorManager.gameObject);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
