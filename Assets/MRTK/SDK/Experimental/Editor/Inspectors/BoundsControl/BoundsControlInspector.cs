﻿//
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Experimental.UI.BoundsControl;
using Microsoft.MixedReality.Toolkit.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;

namespace Microsoft.MixedReality.Toolkit.Experimental.Inspectors
{
    [CustomEditor(typeof(BoundsControl), true)]
    [CanEditMultipleObjects]
    public class BoundsControlInspector : UnityEditor.Editor
    {
        private SerializedProperty targetObject;
        private SerializedProperty boundsOverride;
        private SerializedProperty boundsCalculationMethod;
        private SerializedProperty activationType;
        private SerializedProperty controlPadding;
        private SerializedProperty flattenAxis;

        private SerializedProperty smoothingActive;
        private SerializedProperty rotateLerpTime;
        private SerializedProperty scaleLerpTime;

        // Configs
        private SerializedProperty boxDisplayConfiguration;
        private SerializedProperty linksConfiguration;
        private SerializedProperty scaleHandlesConfiguration;
        private SerializedProperty rotationHandlesConfiguration;
        private SerializedProperty proximityEffectConfiguration;

        // Debug
        private SerializedProperty hideElementsInHierarchyEditor;

        // Events
        private SerializedProperty rotateStartedEvent;
        private SerializedProperty rotateStoppedEvent;
        private SerializedProperty scaleStartedEvent;
        private SerializedProperty scaleStoppedEvent;

        private BoundsControl boundsControl;

        private static bool showBoxConfiguration = false;
        private static bool showScaleHandlesConfiguration = false;
        private static bool showRotationHandlesConfiguration = false;
        private static bool showLinksConfiguration = false;
        private static bool showProximityConfiguration = false;
        private static bool constraintsFoldout = true;

        private void OnEnable()
        {
            boundsControl = (BoundsControl)target;

            targetObject = serializedObject.FindProperty("targetObject");
            activationType = serializedObject.FindProperty("activation");
            boundsOverride = serializedObject.FindProperty("boundsOverride");
            boundsCalculationMethod = serializedObject.FindProperty("boundsCalculationMethod");
            flattenAxis = serializedObject.FindProperty("flattenAxis");
            controlPadding = serializedObject.FindProperty("boxPadding");

            smoothingActive = serializedObject.FindProperty("smoothingActive");
            rotateLerpTime = serializedObject.FindProperty("rotateLerpTime");
            scaleLerpTime = serializedObject.FindProperty("scaleLerpTime");

            boxDisplayConfiguration = serializedObject.FindProperty("boxDisplayConfiguration");
            linksConfiguration = serializedObject.FindProperty("linksConfiguration");
            scaleHandlesConfiguration = serializedObject.FindProperty("scaleHandlesConfiguration");
            rotationHandlesConfiguration = serializedObject.FindProperty("rotationHandlesConfiguration");
            proximityEffectConfiguration = serializedObject.FindProperty("handleProximityEffectConfiguration");

            hideElementsInHierarchyEditor = serializedObject.FindProperty("hideElementsInInspector");

            rotateStartedEvent = serializedObject.FindProperty("rotateStarted");
            rotateStoppedEvent = serializedObject.FindProperty("rotateStopped");
            scaleStartedEvent = serializedObject.FindProperty("scaleStarted");
            scaleStoppedEvent = serializedObject.FindProperty("scaleStopped");
        }

        public override void OnInspectorGUI()
        {
            if (target != null)
            {
                // Notification section - first thing to show in bounds control component
                DrawRigidBodyWarning();

                // Help url
                InspectorUIUtility.RenderHelpURL(target.GetType());

                // Data section
                {
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(targetObject);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(new GUIContent("Behavior"), EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(activationType);
                    EditorGUILayout.PropertyField(boundsOverride);
                    EditorGUILayout.PropertyField(boundsCalculationMethod);
                    EditorGUILayout.PropertyField(controlPadding);
                    EditorGUILayout.PropertyField(flattenAxis);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(new GUIContent("Smoothing"), EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(smoothingActive);
                    EditorGUILayout.PropertyField(scaleLerpTime);
                    EditorGUILayout.PropertyField(rotateLerpTime);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(new GUIContent("Visuals", "Bounds Control Visual Configurations"), EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                    using (new EditorGUI.IndentLevelScope())
                    {

                        showBoxConfiguration = InspectorUIUtility.DrawScriptableFoldout<BoxDisplayConfiguration>(boxDisplayConfiguration, 
                                                                                                                 "Box Configuration", 
                                                                                                                 showBoxConfiguration);
                        showScaleHandlesConfiguration = InspectorUIUtility.DrawScriptableFoldout<ScaleHandlesConfiguration>(scaleHandlesConfiguration, 
                                                                                                                            "Scale Handles Configuration", 
                                                                                                                            showScaleHandlesConfiguration);
                        showRotationHandlesConfiguration = InspectorUIUtility.DrawScriptableFoldout<RotationHandlesConfiguration>(rotationHandlesConfiguration, 
                                                                                                                                  "Rotation Handles Configuration", 
                                                                                                                                  showRotationHandlesConfiguration);
                        showLinksConfiguration = InspectorUIUtility.DrawScriptableFoldout<LinksConfiguration>(linksConfiguration, 
                                                                                                              "Links Configuration", 
                                                                                                              showLinksConfiguration);
                        showProximityConfiguration = InspectorUIUtility.DrawScriptableFoldout<ProximityEffectConfiguration>(proximityEffectConfiguration, 
                                                                                                                            "Proximity Configuration", 
                                                                                                                            showProximityConfiguration);
                    }

                    EditorGUILayout.Space();
                    constraintsFoldout = InspectorUIUtility.DrawComponentTypeFoldout<TransformConstraint>(boundsControl.gameObject, constraintsFoldout, "Constraint");

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(new GUIContent("Events", "Bounds Control Events"), EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                    {
                        EditorGUILayout.PropertyField(rotateStartedEvent);
                        EditorGUILayout.PropertyField(rotateStoppedEvent);
                        EditorGUILayout.PropertyField(scaleStartedEvent);
                        EditorGUILayout.PropertyField(scaleStoppedEvent);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(new GUIContent("Debug", "Bounds Control Debug Section"), EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                    {
                        EditorGUILayout.PropertyField(hideElementsInHierarchyEditor);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }
        }

        private void DrawRigidBodyWarning()
        {
            // Check if rigidbody is attached - if so show warning in case input profile is not configured for individual collider raycast
            Rigidbody rigidBody = boundsControl.GetComponent<Rigidbody>();

            if (rigidBody != null)
            {
                MixedRealityInputSystemProfile profile = Microsoft.MixedReality.Toolkit.CoreServices.InputSystem?.InputSystemProfile;
                if (profile != null && profile.FocusIndividualCompoundCollider == false)
                {
                    EditorGUILayout.Space();
                    // Show warning and button to reconfigure profile
                    EditorGUILayout.HelpBox($"When using Bounds Control in combination with Rigidbody 'Focus Individual Compound Collider' must be enabled in Input Profile.", UnityEditor.MessageType.Warning);
                    if (GUILayout.Button($"Enable 'Focus Individual Compound Collider' in Input Profile"))
                    {
                        profile.FocusIndividualCompoundCollider = true;
                    }

                    EditorGUILayout.Space();
                }
            }
        }
    }
}
