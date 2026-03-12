using Core.Common.Attributes;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// 에디터 전용 어셈블리(Assembly-CSharp-Editor) 생성을 보장하기 위한 앵커 클래스.
    /// </summary>
    internal static class EditorAssemblyAnchor
    {
    }

    /// <summary>
    /// MinMaxRangeAttribute가 적용된 Vector2를 2핸들 슬라이더로 그린다.
    /// </summary>
    [CustomPropertyDrawer(typeof(MinMaxRangeAttribute))]
    internal sealed class MinMaxRangeAttributeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            MinMaxRangeAttribute range = (MinMaxRangeAttribute)attribute;

            if (property.propertyType != SerializedPropertyType.Vector2 || !range.ShowFields)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            MinMaxRangeAttribute range = (MinMaxRangeAttribute)attribute;
            Vector2 value = property.vector2Value;
            float minValue = Mathf.Clamp(value.x, range.Min, range.Max);
            float maxValue = Mathf.Clamp(value.y, range.Min, range.Max);

            if (maxValue < minValue)
            {
                maxValue = minValue;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect sliderRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);

            Rect contentRect = EditorGUI.PrefixLabel(sliderRect, label);
            EditorGUI.MinMaxSlider(contentRect, ref minValue, ref maxValue, range.Min, range.Max);

            if (range.ShowFields)
            {
                Rect fieldsRect = new Rect(
                    position.x,
                    position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    EditorGUIUtility.singleLineHeight);

                float halfWidth = (fieldsRect.width - 6f) * 0.5f;
                Rect startRect = new Rect(fieldsRect.x, fieldsRect.y, halfWidth, fieldsRect.height);
                Rect endRect = new Rect(fieldsRect.x + halfWidth + 6f, fieldsRect.y, halfWidth, fieldsRect.height);

                minValue = EditorGUI.FloatField(startRect, "Start", minValue);
                maxValue = EditorGUI.FloatField(endRect, "End", maxValue);
            }

            minValue = Mathf.Clamp(minValue, range.Min, range.Max);
            maxValue = Mathf.Clamp(maxValue, minValue, range.Max);
            property.vector2Value = new Vector2(minValue, maxValue);

            EditorGUI.EndProperty();
        }
    }
}
