using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(HoleData))]
public class HoleDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Get properties
        SerializedProperty holeName = property.FindPropertyRelative("holeName");
        SerializedProperty type = property.FindPropertyRelative("type");
        SerializedProperty location = property.FindPropertyRelative("location");
        SerializedProperty radius = property.FindPropertyRelative("radius");
        SerializedProperty width = property.FindPropertyRelative("width");
        SerializedProperty height = property.FindPropertyRelative("height");
        SerializedProperty angleDegrees = property.FindPropertyRelative("angleDegrees");
        SerializedProperty heightPosition = property.FindPropertyRelative("heightPosition");
        SerializedProperty bottomOffset = property.FindPropertyRelative("bottomOffset");

        // Draw an expandable foldout label for each hole instance
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, holeName.stringValue, true);
        
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float yOffset = position.y + EditorGUIUtility.singleLineHeight + 2;

            // 1. Hole Custom Name Name Field
            EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), holeName);
            yOffset += EditorGUIUtility.singleLineHeight + 2;

            // 2. Shape Type Selector (Circular / Rectangular)
            EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), type);
            yOffset += EditorGUIUtility.singleLineHeight + 2;

            // 3. Location Selector (Side / Bottom)
            EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), location);
            yOffset += EditorGUIUtility.singleLineHeight + 2;

            // DYNAMIC SHAPE FILTERING
            if (type.enumValueIndex == (int)BucketHoleCutter.HoleType.Circular)
            {
                // Show ONLY Radius if Circular
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), radius);
                yOffset += EditorGUIUtility.singleLineHeight + 2;
            }
            else if (type.enumValueIndex == (int)BucketHoleCutter.HoleType.Rectangular)
            {
                // Show ONLY Width and Height if Rectangular
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), width);
                yOffset += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), height);
                yOffset += EditorGUIUtility.singleLineHeight + 2;
            }

            // DYNAMIC LOCATION FILTERING
            if (location.enumValueIndex == (int)BucketHoleCutter.HoleLocation.Side)
            {
                // Show ONLY Side Controls (Angle and Vertical Height)
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), angleDegrees);
                yOffset += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), heightPosition);
            }
            else if (location.enumValueIndex == (int)BucketHoleCutter.HoleLocation.Bottom)
            {
                // Show ONLY Bottom Controls (2D Planar Offset Vector)
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), bottomOffset);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        // Calculate dynamic height depending on visible fields to avoid blank spaces
        float lines = 4; // Foldout, Name, Type, Location are always visible

        SerializedProperty type = property.FindPropertyRelative("type");
        SerializedProperty location = property.FindPropertyRelative("location");

        if (type.enumValueIndex == (int)BucketHoleCutter.HoleType.Circular) lines += 1; // radius
        else lines += 2; // width + height

        if (location.enumValueIndex == (int)BucketHoleCutter.HoleLocation.Side) lines += 2; // angle + height position
        else lines += 1; // bottom offset vector

        return lines * EditorGUIUtility.singleLineHeight + (lines * 2);
    }
}