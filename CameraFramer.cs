using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Adalomoon;

/// <summary>
/// Applies a brief, bounded framing adjustment to the normal game camera.
/// It never changes min/max camera limits or uses hooks/signature scans.
/// </summary>
internal sealed unsafe class CameraFramer
{
    private const float TurnRightRadians = 0.48f;
    private const float TransitionSeconds = 0.85f;
    private bool active;
    private DateTime startedAt;
    private Camera* camera;
    private float originalDistance;
    private float originalYaw;
    private float targetDistance;
    private float targetYaw;

    public void Begin()
    {
        if (active)
            return;

        var manager = CameraManager.Instance();
        if (manager is null || manager->Camera is null)
            return;

        camera = manager->Camera;
        if (camera->ControlMode == CameraControlMode.FirstPerson)
            return;

        originalDistance = camera->Distance;
        originalYaw = camera->DirH;
        if (!float.IsFinite(originalDistance) || !float.IsFinite(originalYaw) ||
            !float.IsFinite(camera->MinDistance) || !float.IsFinite(camera->MaxDistance) ||
            camera->MinDistance > camera->MaxDistance)
            return;

        // The target is the closest distance the game itself currently permits.
        targetDistance = camera->MinDistance;
        targetYaw = WrapAngle(originalYaw + TurnRightRadians);
        startedAt = DateTime.UtcNow;
        active = true;
    }

    public void Update(IFramework framework)
    {
        if (!active || camera is null)
            return;

        var manager = CameraManager.Instance();
        if (manager is null || manager->Camera != camera)
        {
            active = false;
            camera = null;
            return;
        }

        var elapsed = (float)(DateTime.UtcNow - startedAt).TotalSeconds;
        var t = Math.Clamp(elapsed / TransitionSeconds, 0f, 1f);
        // Smoothstep keeps the adjustment unobtrusive.
        t = t * t * (3f - 2f * t);
        camera->Distance = Math.Clamp(Lerp(originalDistance, targetDistance, t), camera->MinDistance, camera->MaxDistance);
        camera->DirH = LerpAngle(originalYaw, targetYaw, t);
    }

    public void Restore()
    {
        var manager = CameraManager.Instance();
        if (active && camera is not null && manager is not null && manager->Camera == camera)
        {
            camera->Distance = Math.Clamp(originalDistance, camera->MinDistance, camera->MaxDistance);
            camera->DirH = originalYaw;
        }

        active = false;
        camera = null;
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * t);

    private static float LerpAngle(float from, float to, float t)
    {
        var delta = WrapAngle(to - from);
        return WrapAngle(from + (delta * t));
    }

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle < -MathF.PI) angle += MathF.Tau;
        return angle;
    }
}

