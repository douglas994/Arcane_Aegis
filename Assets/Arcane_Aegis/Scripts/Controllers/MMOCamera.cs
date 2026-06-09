using System;
using System.Collections.Generic;
using UnityEngine;
using MMO.Scripts.Controllers;
using Arcane_Aegis.Controllers.Inputs;

namespace MMO.Scripts.Controllers
{
    public class MMOCamera : MonoBehaviour
    {
        [Header("Framing")]
        public Transform Target;
        public Camera Camera;
        public Vector2 FollowPointFraming = new Vector2(0f, 0f);
        public float FollowingSharpness = 10000f;

        [Header("Distance")]
        public float DefaultDistance = 6f;
        public float MinDistance = 0f;
        public float MaxDistance = 10f;
        public float DistanceMovementSpeed = 5f;
        public float DistanceMovementSharpness = 10f;

        [Header("Rotation")]
        public bool InvertX = false;
        public bool InvertY = false;
        [Range(-90f, 90f)]
        public float DefaultVerticalAngle = 20f;
        [Range(-90f, 90f)]
        public float MinVerticalAngle = -90f;
        [Range(-90f, 90f)]
        public float MaxVerticalAngle = 90f;
        public float RotationSpeed = 1f;
        public float RotationSharpness = 10000f;
        public bool RotateWithPhysicsMover = false;

        [Header("Obstruction")]
        public float ObstructionCheckRadius = 0.2f;
        public LayerMask ObstructionLayers = -1;
        public float ObstructionSharpness = 10000f;
        public List<Collider> IgnoredColliders = new List<Collider>();

        public Transform Transform { get; private set; }
        public Transform FollowTransform { get; private set; }

        public Vector3 PlanarDirection { get; set; }
        public float TargetDistance { get; set; }

        private bool _distanceIsObstructed;
        private float _currentDistance;
        private float _targetVerticalAngle;
        private RaycastHit _obstructionHit;
        private int _obstructionCount;
        private RaycastHit[] _obstructions = new RaycastHit[MaxObstructions];
        private float _obstructionTime;
        private Vector3 _currentFollowPosition;
        private PlayerInput _playerInput;

        private const int MaxObstructions = 32;

        void OnValidate()
        {
            DefaultDistance = Mathf.Clamp(DefaultDistance, MinDistance, MaxDistance);
            DefaultVerticalAngle = Mathf.Clamp(DefaultVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
        }

        void Awake()
        {
            Transform = this.transform;

            _currentDistance = DefaultDistance;
            TargetDistance = _currentDistance;

            _targetVerticalAngle = 0f;

            PlanarDirection = Vector3.forward;
        }

        private void FindAndSetPlayerTarget()
        {
            // Procura por um GameObject com tag "Player"
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                // Procura pelo Target como child do Player
                Transform targetTransform = playerObj.transform.Find("Target") ?? FindDeep(playerObj.transform, "Target");
                if (targetTransform != null)
                {
                    SetFollowTransform(targetTransform);
                    _playerInput = playerObj.GetComponentInChildren<PlayerInput>(); // bind to the LOCAL player's input
                    UnityEngine.Debug.Log("[MMOCamera] Target encontrado dentro do Player!");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[MMOCamera] Target não encontrado dentro do Player! (crie um child vazio chamado exatamente 'Target')");
                }
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) return c;
                Transform r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        private void LateUpdate()
        {
            // Find the local Player (tag "Player") once it spawns — binds its Target + PlayerInput together.
            if (FollowTransform == null)
            {
                FindAndSetPlayerTarget();
            }
            
            HandleCameraInput();
        }

        private void HandleCameraInput()
        {
            // Valida PlayerInput
            if (_playerInput == null)
                return;

            Vector3 lookInputVector = Vector3.zero;
            if(_playerInput.RightClick)
            {
                // Create the look input vector for the camera
                float mouseLookAxisUp = _playerInput.Look.y;
                float mouseLookAxisRight = _playerInput.Look.x;
                lookInputVector = new Vector3(mouseLookAxisRight, mouseLookAxisUp, 0f);
            }
            

            // Input for zooming the camera (disabled in WebGL because it can cause problems)
            float scrollInput = -_playerInput.Zoom;
#if UNITY_WEBGL
        scrollInput = 0f;
#endif

            // Apply inputs to the camera
            UpdateWithInput(Time.deltaTime, scrollInput, lookInputVector);

            
        }



        // Set the transform that the camera will orbit around
        public void SetFollowTransform(Transform t)
        {
            if (t == null)
            {
                UnityEngine.Debug.LogWarning("[MMOCamera] Tentou setar FollowTransform nulo!");
                return;
            }
            FollowTransform = t;
            PlanarDirection = FollowTransform.forward;
            _currentFollowPosition = FollowTransform.position;
        }

        public void UpdateWithInput(float deltaTime, float zoomInput, Vector3 rotationInput)
        {
            if (FollowTransform)
            {
                if (InvertX)
                {
                    rotationInput.x *= -1f;
                }
                if (InvertY)
                {
                    rotationInput.y *= -1f;
                }

                // Process rotation input
                Quaternion rotationFromInput = Quaternion.Euler(FollowTransform.up * (rotationInput.x * RotationSpeed));
                PlanarDirection = rotationFromInput * PlanarDirection;
                PlanarDirection = Vector3.Cross(FollowTransform.up, Vector3.Cross(PlanarDirection, FollowTransform.up));
                Quaternion planarRot = Quaternion.LookRotation(PlanarDirection, FollowTransform.up);

                _targetVerticalAngle -= (rotationInput.y * RotationSpeed);
                _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
                Quaternion verticalRot = Quaternion.Euler(_targetVerticalAngle, 0, 0);
                Quaternion targetRotation = Quaternion.Slerp(Transform.rotation, planarRot * verticalRot, 1f - Mathf.Exp(-RotationSharpness * deltaTime));

                // Apply rotation
                Transform.rotation = targetRotation;

                // Process distance input
                if (_distanceIsObstructed && Mathf.Abs(zoomInput) > 0f)
                {
                    TargetDistance = _currentDistance;
                }
                TargetDistance += zoomInput * DistanceMovementSpeed;
                TargetDistance = Mathf.Clamp(TargetDistance, MinDistance, MaxDistance);

                // Find the smoothed follow position
                _currentFollowPosition = Vector3.Lerp(_currentFollowPosition, FollowTransform.position, 1f - Mathf.Exp(-FollowingSharpness * deltaTime));

                // Handle obstructions
                {
                    RaycastHit closestHit = new RaycastHit();
                    closestHit.distance = Mathf.Infinity;
                    _obstructionCount = Physics.SphereCastNonAlloc(_currentFollowPosition, ObstructionCheckRadius, -Transform.forward, _obstructions, TargetDistance, ObstructionLayers, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < _obstructionCount; i++)
                    {
                        bool isIgnored = false;
                        for (int j = 0; j < IgnoredColliders.Count; j++)
                        {
                            if (IgnoredColliders[j] == _obstructions[i].collider)
                            {
                                isIgnored = true;
                                break;
                            }
                        }

                        if (!isIgnored && _obstructions[i].distance < closestHit.distance && _obstructions[i].distance > 0)
                        {
                            closestHit = _obstructions[i];
                        }
                    }

                    // If obstructions detected
                    if (closestHit.distance < Mathf.Infinity)
                    {
                        _distanceIsObstructed = true;
                        _currentDistance = Mathf.Lerp(_currentDistance, closestHit.distance, 1 - Mathf.Exp(-ObstructionSharpness * deltaTime));
                    }
                    // If no obstruction
                    else
                    {
                        _distanceIsObstructed = false;
                        _currentDistance = Mathf.Lerp(_currentDistance, TargetDistance, 1 - Mathf.Exp(-DistanceMovementSharpness * deltaTime));
                    }
                }

                // Find the smoothed camera orbit position
                Vector3 targetPosition = _currentFollowPosition - ((targetRotation * Vector3.forward) * _currentDistance);

                // Handle framing
                targetPosition += Transform.right * FollowPointFraming.x;
                targetPosition += Transform.up * FollowPointFraming.y;

                // Apply position
                Transform.position = targetPosition;
            }
        }
    }
}