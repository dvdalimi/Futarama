﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.Toolkit.Experimental.UI.BoundsControl
{
    /// <summary>
    /// Configuration base class for any <see cref="BoundsControl"/> handle type deriving from <see cref="HandlesBase"/>
    /// </summary>
    public abstract class HandlesBaseConfiguration : ScriptableObject
    { 
        [SerializeField]
        [Tooltip("Material applied to handles when they are not in a grabbed state")]
        private Material handleMaterial;

        /// <summary>
        /// Material applied to handles when they are not in a grabbed state
        /// </summary>
        public Material HandleMaterial
        {
            get { return handleMaterial; }
            set
            {
                if (handleMaterial != value)
                {
                    handleMaterial = value;
                    TrySetDefaultMaterial();
                    handlesChanged.Invoke(HandlesChangedEventType.MATERIAL);
                   // configurationChanged.Invoke();
                }
            }
        }

        [SerializeField]
        [Tooltip("Material applied to handles while they are a grabbed")]
        private Material handleGrabbedMaterial;

        /// <summary>
        /// Material applied to handles while they are a grabbed
        /// </summary>
        public Material HandleGrabbedMaterial
        {
            get { return handleGrabbedMaterial; }
            set
            {
                if (handleGrabbedMaterial != value)
                {
                    handleGrabbedMaterial = value;
                    TrySetDefaultMaterial();
                    handlesChanged.Invoke(HandlesChangedEventType.MATERIAL_GRABBED);
                    //configurationChanged.Invoke();
                }
            }
        }

        [SerializeField]
        [Tooltip("Prefab used to display this type of bounds control handle. If not set, default shape will be used (scale default: boxes, rotation default: spheres)")]
        GameObject handlePrefab = null;

        /// <summary>
        /// Prefab used to display this type of bounds control handle. If not set, default shape will be used (scale default: boxes, rotation default: spheres)
        /// </summary>
        public GameObject HandlePrefab
        {
            get { return handlePrefab; }
            set
            {
                if (handlePrefab != value)
                {
                    handlePrefab = value;
                    handlesChanged.Invoke(HandlesChangedEventType.PREFAB);
                    //configurationChanged.Invoke();
                }
            }
        }

        [SerializeField]
        [Tooltip("Size of the handle collidable")]
        private float handleSize = 0.016f; // 1.6cm default handle size

        /// <summary>
        /// Size of the handle collidable
        /// </summary>
        public float HandleSize
        {
            get { return handleSize; }
            set
            {
                if (handleSize != value)
                {
                    handleSize = value;
                    handlesChanged.Invoke(HandlesChangedEventType.COLLIDER_SIZE);
                    //configurationChanged.Invoke();
                }
            }
        }

        [SerializeField]
        [Tooltip("Additional padding to apply to the handle collider to make handle easier to hit")]
        private Vector3 colliderPadding = new Vector3(0.016f, 0.016f, 0.016f);

        /// <summary>
        /// Additional padding to apply to the handle collider to make handle easier to hit
        /// </summary>
        public Vector3 ColliderPadding
        {
            get { return colliderPadding; }
            set
            {
                if (colliderPadding != value)
                {
                    colliderPadding = value;
                    handlesChanged.Invoke(HandlesChangedEventType.COLLIDER_PADDING);
                }
            }
        }

        [SerializeField]
        [Tooltip("Check to draw a tether point from the handles to the hand when manipulating.")]
        private bool drawTetherWhenManipulating = true;

        /// <summary>
        /// Check to draw a tether point from the handles to the hand when manipulating.
        /// </summary>
        public bool DrawTetherWhenManipulating
        {
            get => drawTetherWhenManipulating;
            set
            {
                if (value != drawTetherWhenManipulating)
                {
                    drawTetherWhenManipulating = value;
                    handlesChanged.Invoke(HandlesChangedEventType.MANIPULATION_TETHER);
                }
            }
            
        }

        [SerializeField]
        [Tooltip("Add a Collider here if you do not want the handle colliders to interact with another object's collider.")]
        private Collider handlesIgnoreCollider = null;

        /// <summary>
        /// Add a Collider here if you do not want the handle colliders to interact with another object's collider.
        /// </summary>
        public Collider HandlesIgnoreCollider
        {
            get => handlesIgnoreCollider;
            set
            {
                if (value != handlesIgnoreCollider)
                {
                    handlesChanged.Invoke(HandlesChangedEventType.IGNORE_COLLIDER_REMOVE);
                    handlesIgnoreCollider = value;
                    handlesChanged.Invoke(HandlesChangedEventType.IGNORE_COLLIDER_ADD);
                }
            }
        }

        //internal protected UnityEvent configurationChanged = new UnityEvent();
        //internal protected UnityEvent visibilityChanged = new UnityEvent();

        internal enum HandlesChangedEventType
        {
            MATERIAL,
            MATERIAL_GRABBED,
            PREFAB,
            COLLIDER_SIZE,
            COLLIDER_PADDING,
            MANIPULATION_TETHER,
            IGNORE_COLLIDER_REMOVE,
            IGNORE_COLLIDER_ADD,
            VISIBILITY
        }
        internal class HandlesChangedEvent : UnityEvent<HandlesChangedEventType> { }
        internal HandlesChangedEvent handlesChanged = new HandlesChangedEvent();

        private void Awake()
        {
            TrySetDefaultMaterial();
        }

        internal protected void TrySetDefaultMaterial()
        {
            if (handleMaterial == null)
            {
                handleMaterial = VisualUtils.CreateDefaultMaterial();
            }
            if (handleGrabbedMaterial == null && handleGrabbedMaterial != handleMaterial)
            {
                handleGrabbedMaterial = VisualUtils.CreateDefaultMaterial();
            }
        }


    }
}
