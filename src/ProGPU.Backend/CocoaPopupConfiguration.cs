namespace ProGPU.Backend;

internal interface ICocoaPopupOperations
{
    bool HasCurrentHostIdentity { get; }
    bool IsVisible(nint window);
    nint GetParent(nint window);
    bool GetHidesOnDeactivate(nint window);
    void SetHidesOnDeactivate(nint window, bool value);
    void RemoveChild(nint owner, nint child);
    void AddChild(nint owner, nint child);
    void Hide(nint window);
}

// Ownership only: an NSWindow child is NOT admitted to an AppKit modal session.
internal static class CocoaPopupConfiguration
{
    internal static bool Prepare<TOperations>(nint owner, nint popup, ref TOperations operations)
        where TOperations : ICocoaPopupOperations =>
        Admit(owner, popup, ref operations) && !operations.IsVisible(popup);

    private static bool Admit<TOperations>(nint owner, nint popup, ref TOperations operations)
        where TOperations : ICocoaPopupOperations
    {
        if (owner == 0 || popup == 0 || owner == popup || !operations.HasCurrentHostIdentity)
            return false;
        nint parent = operations.GetParent(popup);
        if (parent != 0 && parent != owner) return false;
        for (nint ancestor = owner, depth = 0; ancestor != 0; ++depth, ancestor = operations.GetParent(ancestor))
            if (depth == 1024 || ancestor == popup) return false;
        return true;
    }

    internal static bool Show<TOperations>(nint owner, nint popup, ref TOperations operations, Action show)
        where TOperations : ICocoaPopupOperations
    {
        if (!Admit(owner, popup, ref operations) ||
            (operations.IsVisible(popup) && operations.GetParent(popup) != owner)) return false;
        bool hides = operations.GetHidesOnDeactivate(popup);
        bool applied = false;
        try
        {
            operations.SetHidesOnDeactivate(popup, false);
            if (!Admit(owner, popup, ref operations) || operations.GetHidesOnDeactivate(popup)) return false;
            if (operations.GetParent(popup) == 0)
            {
                if (operations.IsVisible(popup)) return false;
                // AppKit makes the child visible here. This is the native Show
                // boundary, after hidden preparation and host input admission.
                operations.AddChild(owner, popup);
            }
            if (!operations.HasCurrentHostIdentity || operations.GetParent(popup) != owner) return false;
            show();
            applied = operations.HasCurrentHostIdentity && operations.IsVisible(popup) &&
                operations.GetParent(popup) == owner && !operations.GetHidesOnDeactivate(popup);
            return applied;
        }
        finally
        {
            if (!applied && operations.HasCurrentHostIdentity &&
                (operations.GetParent(popup) == owner || operations.GetParent(popup) == 0))
            {
                // orderOut detaches the child. Restoring a previous parent would
                // show it again, so rejected hosts are left hidden for disposal.
                operations.Hide(popup);
                if (operations.HasCurrentHostIdentity && !operations.IsVisible(popup) && operations.GetParent(popup) == 0)
                    operations.SetHidesOnDeactivate(popup, hides);
            }
        }
    }

    internal static bool Apply<TOperations>(nint owner, nint popup, ref TOperations operations)
        where TOperations : ICocoaPopupOperations
    {
        if (owner == 0 || popup == 0 || owner == popup ||
            !operations.HasCurrentHostIdentity || operations.IsVisible(popup)) return false;
        nint ancestor = owner;
        for (int depth = 0; ancestor != 0; ++depth, ancestor = operations.GetParent(ancestor))
            if (depth == 1024 || ancestor == popup) return false;

        nint previous = operations.GetParent(popup);
        bool hides = operations.GetHidesOnDeactivate(popup);
        bool applied = false;
        try
        {
            if (previous != owner)
            {
                if (previous != 0) operations.RemoveChild(previous, popup);
                if (!operations.HasCurrentHostIdentity || operations.IsVisible(popup) || operations.GetParent(popup) != 0)
                    return false;
                operations.AddChild(owner, popup);
            }
            if (!operations.HasCurrentHostIdentity || operations.IsVisible(popup) || operations.GetParent(popup) != owner)
                return false;
            operations.SetHidesOnDeactivate(popup, false);
            applied = operations.HasCurrentHostIdentity && !operations.IsVisible(popup) &&
                operations.GetParent(popup) == owner && !operations.GetHidesOnDeactivate(popup);
            return applied;
        }
        finally
        {
            if (!applied && operations.HasCurrentHostIdentity && !operations.IsVisible(popup))
            {
                nint current = operations.GetParent(popup);
                // Do not overwrite a third party's reentrant ownership change.
                if (current == owner || current == 0 || current == previous)
                {
                    if (current != previous)
                    {
                        if (current != 0) operations.RemoveChild(current, popup);
                        if (previous != 0) operations.AddChild(previous, popup);
                    }
                    operations.SetHidesOnDeactivate(popup, hides);
                }
            }
            // Any failure still requires destruction of the hidden popup by its
            // owning host, including a rejected or interrupted native rollback.
        }
    }
}
