using UnityEditor;
using UnityEngine;

namespace GatorDragonGames.JigglePhysics {

[CustomPropertyDrawer(typeof(JiggleCollider))]
public class JiggleColliderPropertyDrawer : PropertyDrawer {
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        var typeProp = property.FindPropertyRelative("type");
        var type = (JiggleCollider.JiggleColliderType)typeProp.enumValueIndex;
        int lines = 2; // type + at least one field
        switch (type) {
            case JiggleCollider.JiggleColliderType.Sphere:
                lines = 2; // type, radius
                break;
            case JiggleCollider.JiggleColliderType.Capsule:
                lines = 4; // type, radius, height, capsuleAxis
                break;
            case JiggleCollider.JiggleColliderType.Plane:
                lines = 1; // type only
                break;
            case JiggleCollider.JiggleColliderType.Box:
                lines = 2; // type, boxExtents
                break;
        }
        return lines * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);

        var typeProp = property.FindPropertyRelative("type");
        var radiusProp = property.FindPropertyRelative("radius");
        var heightProp = property.FindPropertyRelative("height");
        var capsuleAxisProp = property.FindPropertyRelative("capsuleAxis");
        var boxExtentsProp = property.FindPropertyRelative("boxExtents");

        var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(rect, typeProp);
        rect.y += lineHeight;

        var type = (JiggleCollider.JiggleColliderType)typeProp.enumValueIndex;

        switch (type) {
            case JiggleCollider.JiggleColliderType.Sphere:
                ClampedFloatField(rect, radiusProp, "Radius");
                break;
            case JiggleCollider.JiggleColliderType.Capsule:
                ClampedFloatField(rect, radiusProp, "Radius");
                rect.y += lineHeight;
                ClampedFloatField(rect, heightProp, "Height");
                rect.y += lineHeight;
                EditorGUI.PropertyField(rect, capsuleAxisProp, new GUIContent("Axis"));
                break;
            case JiggleCollider.JiggleColliderType.Plane:
                // No extra fields
                break;
            case JiggleCollider.JiggleColliderType.Box:
                ClampedVector3Field(rect, boxExtentsProp, "Half Extents");
                break;
        }

        EditorGUI.EndProperty();
    }

    private void ClampedFloatField(Rect rect, SerializedProperty prop, string label) {
        EditorGUI.PropertyField(rect, prop, new GUIContent(label));
        if (prop.floatValue < 0f) {
            prop.floatValue = 0f;
        }
    }

    private void ClampedVector3Field(Rect rect, SerializedProperty prop, string label) {
        EditorGUI.PropertyField(rect, prop, new GUIContent(label));
        var x = prop.FindPropertyRelative("x");
        var y = prop.FindPropertyRelative("y");
        var z = prop.FindPropertyRelative("z");
        if (x.floatValue < 0f) x.floatValue = 0f;
        if (y.floatValue < 0f) y.floatValue = 0f;
        if (z.floatValue < 0f) z.floatValue = 0f;
    }
}

}
