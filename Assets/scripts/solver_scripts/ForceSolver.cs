﻿using System;
using System.Diagnostics;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities.Solvers;
using Microsoft.MixedReality.Toolkit.WindowsMixedReality.Input;
using UnityEngine;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

public class ForceSolver : Solver, IMixedRealityFocusHandler, IMixedRealityPointerHandler
{
    public enum State
    {
        Root,
        Free,
        Attraction,
        Manipulation,
        Rejection
    }

    private ManipulationHandler _manipulationHandler;
    private Collider _attractionCollider;
    private Quaternion _rotationOffset, _previousRotation;
    
    public State ForceState { get; private set; }
    public Transform RootTransform;
    public ControllerTransformTracker ControllerTracker;
    public bool OffsetToObjectBoundsFromController = true;
    public Vector3 OffsetFromCameraOnNoControllerPosition;
    public ManipulationHandler ManipulationHandler;
    public Collider AttractionCollider;

    public UnityEvent SetToRoot, SetToAttract, SetToManipulate, SetToFree;

    protected override void Awake()
    {
        base.Awake();
        
        _manipulationHandler = ManipulationHandler ?? GetComponentInChildren<ManipulationHandler>();
        Debug.Assert(_manipulationHandler != null, "Force Solver failed to find a manipulation handler");
        _attractionCollider = AttractionCollider ?? GetComponentInChildren<Collider>();
        Debug.Assert(_attractionCollider != null, "Force Solver failed to find a attraction collider");
        
        _manipulationHandler.OnManipulationEnded.AddListener(OnManipulationEnd);

        if (ControllerTracker == null)
        {
            Debug.LogWarning(gameObject.name+" ForceSolver no controller tracker transform supplied will instantiate locally");
            var controllerTrackerObject = new GameObject(gameObject.name+" ForceSolver autogenerated controller tracker");
            ControllerTracker = controllerTrackerObject.AddComponent<ControllerTransformTracker>();
        }

        ControllerTracker.AllTrackingLost += OnControllersLost;
    }

    private void Start()
    {
        StartRoot();
    }

    private void OnDestroy()
    {
        ControllerTracker.AllTrackingLost -= OnControllersLost;
    }

    private void UpdateGoalsAttraction()
    {
        GoalScale = SolverHandler.TransformTarget.localScale;
        GoalPosition = SolverHandler.TransformTarget.position;
        if (OffsetToObjectBoundsFromController && !ControllerTracker.BothSides)
        {
            GoalPosition += GetOffsetPositionFromController();
        }
        GoalRotation = SolverHandler.TransformTarget.rotation * _rotationOffset;
        UpdateWorkingToGoal();
    }

    private Vector3 GetOffsetPositionFromController()
    {
        var controllerFwd = ControllerTracker.transform.forward;
        var ray = new Ray(transform.position-controllerFwd*100, controllerFwd);
        var hit = _attractionCollider.Raycast(ray, out var hitInfo, 150);
        Debug.Assert(hit);
        return transform.position - hitInfo.point;
    }

    private void StartRoot()
    {
        ForceState = State.Root;
        _manipulationHandler.enabled = false;
        SetToRoot?.Invoke();
    }

    private void StartAttraction()
    {
        ForceState = State.Attraction;
        SolverHandler.TransformTarget = ControllerTracker.transform;
        var worldToPalmRotation = Quaternion.Inverse(SolverHandler.TransformTarget.rotation);
        _rotationOffset = worldToPalmRotation * transform.rotation;
        SetToAttract?.Invoke();
    }

    private void StartManipulation()
    {
        ForceState = State.Manipulation;
        _manipulationHandler.enabled = true;
        SetToManipulate?.Invoke();
    }

    private void StartFree()
    {
        ForceState = State.Free;
        _manipulationHandler.enabled = false;
        SetToFree?.Invoke();
    }
    
    private void OnManipulationEnd(ManipulationEventData _)
    {
        StartFree();   
    }

    private void OnControllersLost()
    {
        switch (ForceState)
        {
            case State.Attraction:
                StartRoot();
                break;
        }
    }

    public override void SolverUpdate()
    {
        switch (ForceState)
        {
            case State.Root:
                SnapTo(RootTransform.position, RootTransform.rotation);
                break;
            case State.Free:
                // do nothing
                break;
            case State.Attraction:
                UpdateGoalsAttraction();
                break;
            case State.Manipulation:
                // do nothing
                break;
            case State.Rejection:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void OnBeforeFocusChange(FocusEventData eventData)
    {
        throw new System.NotImplementedException();
    }

    public void OnFocusChanged(FocusEventData eventData)
    {
        throw new System.NotImplementedException();
    }

    public void OnFocusEnter(FocusEventData eventData)
    {
        var controller = eventData.Pointer.Controller;
        // if the focus is the gaze then there is no controller
        if(controller == null) return;
        switch (ForceState)
        {
            case State.Root:
                if (controller == null || !controller.IsInPointingPose || !controller.IsPositionAvailable) return;
                StartAttraction();
                break;
            case State.Attraction:
                if (!controller.IsInPointingPose)
                {
                    StartManipulation();
                }
                break;
            case State.Free:
            case State.Manipulation:
            case State.Rejection:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void OnFocusExit(FocusEventData eventData)
    {
    }

    public void OnPointerUp(MixedRealityPointerEventData eventData)
    {
    }

    public void OnPointerDown(MixedRealityPointerEventData eventData)
    {
        switch (ForceState)
        {
            case State.Root:
                StartAttraction();
                break;
            case State.Attraction:
            case State.Free:
                StartManipulation();
                _manipulationHandler.OnPointerDown(eventData);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
    }

    public void ResetToRoot()
    {
        StartRoot();
    }
}
