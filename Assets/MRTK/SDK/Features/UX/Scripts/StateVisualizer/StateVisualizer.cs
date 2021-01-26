﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[assembly: InternalsVisibleTo("Microsoft.MixedReality.Toolkit.SDK.Editor")]
namespace Microsoft.MixedReality.Toolkit.UI.Interaction
{
    /// <summary>
    /// The State Visualizer component adds animations to an object based on the states defined in a linked Interactive Element component.
    /// This component creates animation assets, places them in the MixedRealityToolkit.Generated folder and enables
    /// simplified animation keyframe setting through adding animatable properties to a target game object.
    /// To enable animation transitions between states, an Animator Controller asset is created and a default state machine
    /// is generated with associated parameters and transitions.  This state machine can be viewed in Unity's Animator window.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class StateVisualizer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("A list of containers that map to the states in the attached Interactive Element component. ")]
        private List<StateContainer> stateContainers = new List<StateContainer>();

        /// <summary>
        /// A list of containers that map to the states in the attached Interactive Element component. 
        /// </summary>
        public List<StateContainer> StateContainers
        {
            get => stateContainers;
            protected set => stateContainers = value;
        }

        [SerializeField]
        [Tooltip("The linked Interactive Element component for this State Visualizer." +
            " The State Visualizer component depends on the presence of a component " +
            " that derives from BaseInteractiveElement.")]
        private BaseInteractiveElement interactiveElement;

        /// <summary>
        /// The linked Interactive Element component for this State Visualizer.
        /// The State Visualizer component depends on the presence of a component 
        /// that derives from BaseInteractiveElement. 
        /// </summary>
        public BaseInteractiveElement InteractiveElement
        {
            get => interactiveElement;
            set => interactiveElement = value;
        }

        [SerializeField]
        [Tooltip("The Animator for this State Visualizer component.  The State Visualizer component" +
            " leverages the capabilities of the Unity animation system and requires the presence of " +
            " an Animator component.")]
        private Animator animator;

        /// <summary>
        /// The Animator for this State Visualizer component.  The State Visualizer component
        /// leverages the capabilities of the Unity animation system and requires the presence of 
        /// an Animator component.
        /// </summary>
        public Animator Animator
        {
            get => animator;
            set => animator = value;
        }

        // The states within an Interactive Element 
        public List<InteractionState> States => InteractiveElement != null ? InteractiveElement.States : null;

        // The state manager within the Interactive Element
        private StateManager stateManager;

        // The animator state machine 
        public AnimatorStateMachine RootStateMachine;

        public AnimatorController AnimatorController;

        private string animationDirectoryPath;

        private void OnValidate()
        {
            if (InteractiveElement == null)
            {
                if (gameObject.GetComponent<BaseInteractiveElement>() != null)
                {
                    InteractiveElement = gameObject.GetComponent<BaseInteractiveElement>();
                }
            }

            if (Animator == null)
            {
                Animator = gameObject.GetComponent<Animator>();
            }

            if (stateContainers.Count == 0)
            {
                InitializeStateContainers();
            }
        }

        private void Start()
        {
            if (InteractiveElement == null)
            {
                InteractiveElement = gameObject.AddComponent<InteractiveElement>();
            }

            stateManager = InteractiveElement.StateManager;

            InitializeStateContainers();

            if (AnimatorController == null)
            {
                InitializeAnimatorControllerAsset();
            }

            stateManager.OnStateActivated.AddListener((state) =>
            {
                Animator.SetTrigger("On" + state.Name);
            });
        }

        #region Animator State Methods

        /// <summary>
        /// Initialize the Animator State Machine by creating new animator states to match the states in Interactive Element. 
        /// </summary>
        /// <param name="animatorController">The animation controller contained in the attached Animator component</param>
        public void SetUpStateMachine(AnimatorController animatorController)
        {
            // Update Animation Clip References
            RootStateMachine = animatorController.layers[0].stateMachine;
            AnimatorController = animatorController;

            foreach (var stateContainer in StateContainers)
            {
                AddNewStateToStateMachine(stateContainer.StateName, animatorController);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Add a new state to the animator state machine and generate a new associated animation clip.
        /// </summary>
        /// <param name="stateName">The name of the new animation state</param>
        /// <param name="animatorController">The animation controller contained in the attached Animator component</param>
        /// <returns>The new animator state in the animator state machine</returns>
        private AnimatorState AddNewStateToStateMachine(string stateName, AnimatorController animatorController)
        {
            // Create animation state
            AnimatorState animatorState = AddAnimatorState(RootStateMachine, stateName);

            // Add associated parameter
            AddAnimatorParameter(animatorController, "On" + stateName, AnimatorControllerParameterType.Trigger);

            // Create and attach animation clip
            AddAnimationClip(animatorState);

            AddAnyStateTransition(RootStateMachine, animatorState);

            StateContainer stateContainer = GetStateContainer(stateName);
            stateContainer.AnimatorStateMachine = RootStateMachine;

            return animatorState;
        }

        private AnimatorState AddAnimatorState(AnimatorStateMachine stateMachine, string animatorStateName)
        {
            bool doesStateExist = Array.Exists(stateMachine.states, (animatorState) => animatorState.state.name == animatorStateName);
            
            if (!doesStateExist)
            {
                return stateMachine.AddState(animatorStateName);
            }
            else
            {
                Debug.LogError($"The {animatorStateName} state already exisits in the animator state machine");
                return null;
            }
        }

        private void AddAnimatorParameter(AnimatorController animatorController, string parameterName, AnimatorControllerParameterType animatorParameterType)
        {
            animatorController.AddParameter(parameterName, animatorParameterType);
        }

        private void AddAnimationClip(AnimatorState animatorState)
        {
            AnimationClip stateAnimationClip = new AnimationClip();
            stateAnimationClip.name = gameObject.name + "_" + animatorState.name + "Clip";

            string animationClipFileName = stateAnimationClip.name + ".anim";

            AssetDatabase.CreateAsset(stateAnimationClip, animationDirectoryPath + "/" + animationClipFileName);

            animatorState.motion = stateAnimationClip;

            StateContainer stateContainer = GetStateContainer(animatorState.name);

            stateContainer.AnimationClip = stateAnimationClip;
        }

        private void AddAnyStateTransition(AnimatorStateMachine animatorStateMachine, AnimatorState animatorState)
        {
            // Idle state
            AnimatorStateTransition transition = animatorStateMachine.AddAnyStateTransition(animatorState);
            transition.name = "To" + animatorState.name;

            // Add Trigger Parameter as a condition for the transition
            transition.AddCondition(AnimatorConditionMode.If, 0, "On" + animatorState.name);
        }

        /// <summary>
        /// Remove an animator state from the state machine.  Used in the StateVisualizerInspector
        /// </summary>
        /// <param name="stateMachine">The state machine for state removal</param>
        /// <param name="animatorStateName">The name of the animator state</param>
        internal void RemoveAnimatorState(AnimatorStateMachine stateMachine, string animatorStateName)
        {
            AnimatorState animatorStateToRemove = GetAnimatorState(animatorStateName);

            stateMachine.RemoveState(animatorStateToRemove);
        }

        /// <summary>
        /// Creates and returns the path to a directory for the animation controller and animation clips assets. 
        /// </summary>
        /// <returns>Returns path to the animation controller and animation clip assets</returns>
        private string CreateAnimationDirectoryPath()
        {
            animationDirectoryPath = Path.Combine("Assets", "MixedRealityToolkit.Generated", "MRTK_Animations");

            // If the animation directory path does not exist, then create a new directory
            if (!Directory.Exists(animationDirectoryPath))
            {
                Directory.CreateDirectory(animationDirectoryPath);
            }

            return animationDirectoryPath;
        }

        // Create a new animator controller asset and add it to the MixedRealityToolkit.Generated folder. 
        // Then set up the state machine for the animator controller.
        internal void InitializeAnimatorControllerAsset()
        {
            // Create MRTK_Animation Directory if it does not exist
            string animationAssetDirectory = CreateAnimationDirectoryPath();
            string animatorControllerName = gameObject.name + ".controller";
            string animationControllerPath = Path.Combine(animationAssetDirectory, animatorControllerName);

            // Create Animation Controller 
            AnimatorController = AnimatorController.CreateAnimatorControllerAtPath(animationControllerPath);

            // Set the runtime animation controller 
            gameObject.GetComponent<Animator>().runtimeAnimatorController = AnimatorController;

            SetUpStateMachine(AnimatorController);
        }

        #endregion

        #region State Container Methods

        private void InitializeStateContainers()
        {
            if (States != null && StateContainers.Count == 0)
            {
                foreach (InteractionState state in States)
                {
                    AddStateContainer(state.Name);
                }
            }
        }

        private void UpdateStateContainers(List<InteractionState> interactionStates)
        {
            if (interactionStates.Count != StateContainers.Count)
            {
                if (interactionStates.Count > StateContainers.Count)
                {
                    foreach (InteractionState state in interactionStates)
                    {
                        // Find the container that matches the state
                        StateContainer container = GetStateContainer(state.Name);

                        if (container == null)
                        {
                            AddStateContainer(state.Name);
                        }
                    }
                }
                else if (interactionStates.Count < StateContainers.Count)
                {
                    foreach (StateContainer stateContainer in StateContainers.ToList())
                    {
                        // Find the state in interactive element for this container
                        InteractionState interactionState = interactionStates.Find((state) => (state.Name == stateContainer.StateName));

                        // Do not remove the default state
                        if (interactionState == null)
                        {
                            RemoveStateContainer(stateContainer.StateName);
                        }
                    }
                }
            }
        }

        private void RemoveStateContainer(string stateName)
        {
            StateContainer containerToRemove = StateContainers.Find((container) => container.StateName == stateName);

            StateContainers.Remove(containerToRemove);
        }

        private void AddStateContainer(string stateName)
        {
            StateContainer stateContainer = new StateContainer(stateName);

            StateContainers.Add(stateContainer);
        }

        /// <summary>
        /// Update the state containers in the state visualizer to match the states in InteractiveElement.  Used in the StateVisualizerInspector.
        /// </summary>
        internal void UpdateStateContainerStates()
        {
            UpdateStateContainers(InteractiveElement.States);

            List<string> stateContainerNames = new List<string>();
            List<string> animatorStateNames = new List<string>();

            // Get state container names
            StateContainers.ForEach((stateContainer) => stateContainerNames.Add(stateContainer.StateName));

            // Get animation state names
            Array.ForEach(RootStateMachine.states, (animatorState) => animatorStateNames.Add(animatorState.state.name));

            // Add new animator state in the root state machine if a state container has been added
            var statesToAdd = stateContainerNames.Except(animatorStateNames);
            foreach (var state in statesToAdd)
            {
                AddNewStateToStateMachine(state, animator.runtimeAnimatorController as AnimatorController);
            }

            // Remove animator state in the root state machine if a state container has been removed
            var statesToRemove = animatorStateNames.Except(stateContainerNames);
            foreach (var stateAni in statesToRemove)
            {
                RemoveAnimatorState(RootStateMachine, stateAni);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get state container given a state name.
        /// </summary>
        /// <param name="stateName">The name of the state container</param>
        /// <returns>The state container with given state name</returns>
        public StateContainer GetStateContainer(string stateName)
        {
            StateContainer stateContainer = StateContainers.Find((container) => container.StateName == stateName);

            return stateContainer != null ? stateContainer : null;
        }

        /// <summary>
        /// Add an animation target to a state container. An animation target contains a reference to 
        /// the target game object and a list of the animatable properties associated with the target.
        /// </summary>
        /// <param name="stateName">The name of the state container</param>
        /// <param name="target">The target game object to add</param>
        /// <returns>The newly created AnimationTarget for a state container</returns>
        public AnimationTarget AddAnimationTargetToState(string stateName, GameObject target)
        {
            StateContainer stateContainer = GetStateContainer(stateName);

            stateContainer.AnimationTargets.Add(new AnimationTarget() { Target = target});

            return stateContainer.AnimationTargets.Last();
        }

        /// <summary>
        /// Add an animatable property to an animation target in a state container.
        /// </summary>
        /// <param name="stateName">The name of the state container</param>
        /// <param name="animationTargetIndex">The index of the animation target in the StateContainer's AnimationTarget list</param>
        /// <param name="animatableProperty">The name of the AnimatableProperty to add</param>
        /// <returns>The new animatable property added</returns>
        public StateAnimatableProperty AddAnimatableProperty(string stateName, int animationTargetIndex, AnimatableProperty animatableProperty)
        {
            return CreateAnimatablePropertyInstance(animationTargetIndex, animatableProperty.ToString(), stateName);
        }

        /// <summary>
        /// Get an animatable property by type.
        /// </summary>
        /// <typeparam name="T">A type that derives from StateAnimatableProperty</typeparam>
        /// <param name="stateName">The name of the state container</param>
        /// <param name="animationTargetIndex">The index of the animation target in the StateContainer's AnimationTarget list</param>
        /// <returns>The animatable property with given type T</returns>
        public T GetAnimatableProperty<T>(string stateName, int animationTargetIndex) where T : StateAnimatableProperty
        {
            StateContainer stateContainer = GetStateContainer(stateName);

            AnimationTarget animationTarget = stateContainer.AnimationTargets[animationTargetIndex];

            IStateAnimatableProperty animatableProperty =  animationTarget.StateAnimatableProperties.Find((animatableProp) => animatableProp is T);

            return animatableProperty as T;
        }

        /// <summary>
        /// Get a list of the shader animatable properties by type. 
        /// </summary>
        /// <typeparam name="T">A type that derives from ShaderStateAnimatableProperty</typeparam>
        /// <param name="stateName">The name of the state container</param>
        /// <param name="animationTargetIndex">The index of the animation target in the StateContainer's AnimationTarget list</param>
        /// <returns>A list of the animatable properties in a container with the given type T</returns>
        public List<T> GetShaderAnimatablePropertyList<T>(string stateName, int animationTargetIndex) where T : ShaderStateAnimatableProperty
        {
            StateContainer stateContainer = GetStateContainer(stateName);

            AnimationTarget animationTarget = stateContainer.AnimationTargets[animationTargetIndex];

            List<T> shaderPropertyList = new List<T>();

            foreach (var animatableProp in animationTarget.StateAnimatableProperties)
            {
                if (animatableProp is T)
                {
                    shaderPropertyList.Add(animatableProp as T);
                }
            }

            return shaderPropertyList;
        }

        /// <summary>
        /// Set the keyframes for a given animatable property. 
        /// </summary>
        /// <param name="stateName">The name of the state container</param>
        /// <param name="animationTargetIndex">The index of the animation target game object</param>
        /// <param name="animatablePropertyName">The name of the animatable property</param>
        public void SetKeyFrames(string stateName, int animationTargetIndex)
        {
            StateContainer stateContainer = GetStateContainer(stateName);

            stateContainer.SetKeyFrames(animationTargetIndex);
        }

        /// <summary>
        /// Remove previously set keyframes. 
        /// </summary>
        /// <param name="stateName">The name of the state container</param>
        /// <param name="animationTargetIndex">The index of the animation target game object</param>
        /// <param name="animatablePropertyName">The name of the animatable property</param>
        public void RemoveKeyFrames(string stateName, int animationTargetIndex, string animatablePropertyName)
        {
            StateContainer stateContainer = GetStateContainer(stateName);

            stateContainer.RemoveKeyFrames(animationTargetIndex, animatablePropertyName);
        }

        /// <summary>
        /// Set the AnimationTransitionDuration for a state.
        /// </summary>
        /// <param name="stateName">The name of the state</param>
        /// <param name="transitionDurationValue">The duration of the transition in seconds</param>
        public void SetAnimationTransitionDuration(string stateName, float transitionDurationValue)
        {
            StateContainer stateContainer = GetStateContainer(stateName);

            if (stateContainer.AnimatorStateMachine == null)
            {
                stateContainer.AnimatorStateMachine = RootStateMachine;
            }

            stateContainer.AnimationTransitionDuration = transitionDurationValue;
        }

        /// <summary>
        /// Set the animation clip for a state.
        /// </summary>
        /// <param name="stateName">The name of the state</param>
        /// <param name="animationClip">The animation clip to set</param>
        public void SetAnimationClip(string stateName, AnimationClip animationClip)
        {
            StateContainer stateContainer = GetStateContainer(stateName);
            stateContainer.AnimationClip = animationClip;
        }

        /// <summary>
        /// Get an animator state in the animator state machine by state name.
        /// </summary>
        /// <param name="animatorStateName">The name of the animator state</param>
        /// <returns>The animator state in the animator state machine</returns>
        public AnimatorState GetAnimatorState(string animatorStateName)
        {
            return Array.Find(RootStateMachine.states, (animatorState) => animatorState.state.name == animatorStateName).state;
        }

        internal StateAnimatableProperty CreateAnimatablePropertyInstance(int animationTargetIndex, string animatablePropertyName, string stateName)
        {
            StateContainer stateContainer = GetStateContainer(stateName);

            if (stateContainer != null)
            {
                return stateContainer.CreateAnimatablePropertyInstance(animationTargetIndex, animatablePropertyName, stateName);
            }
            else
            {
                Debug.LogError($"Could not find a state container with the name {stateName}");
                return null;
            }
        }

        #endregion
    }
}
