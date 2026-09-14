using ProGPU.Backend;
using Xunit;

namespace ProGPU.Tests;

public sealed class CocoaPopupConfigurationTests
{
    [Fact]
    public void PreparationStaysHiddenAndShowAttachesBeforeHostCallbackIncludingReopen()
    {
        var api = new Operations { ShowOnAdd = true };
        Assert.True(CocoaPopupConfiguration.Prepare(1, 2, ref api));
        Assert.Empty(api.Writes);
        Assert.False(api.Visible);
        int shows = 0;
        Action show = () => { Assert.Equal((nint)1, api.Parent); Assert.True(api.Visible); ++shows; };
        Assert.True(CocoaPopupConfiguration.Show(1, 2, ref api, show));
        Assert.True(CocoaPopupConfiguration.Show(1, 2, ref api, show));
        api.Hide(2);
        Assert.True(CocoaPopupConfiguration.Show(1, 2, ref api, show));
        Assert.Equal(3, shows);
        Assert.Equal(2, api.Writes.Count(value => value == "add:1"));
    }

    [Fact]
    public void ThrowingShowHidesAndDetachesBeforePropagating()
    {
        var api = new Operations { ShowOnAdd = true };
        Assert.Throws<InvalidOperationException>(() => CocoaPopupConfiguration.Show(1, 2, ref api,
            () => throw new InvalidOperationException("host show")));
        Assert.False(api.Visible);
        Assert.Equal((nint)0, api.Parent);
        Assert.True(api.Hides);
    }

    [Fact]
    public void RejectedAttachmentNeverInvokesShowOrOverwritesThirdPartyParent()
    {
        var api = new Operations { ShowOnAdd = true };
        api.AfterAdd = () => api.Parent = 4;
        Assert.False(CocoaPopupConfiguration.Show(1, 2, ref api, () => Assert.Fail("unowned show")));
        Assert.Equal((nint)4, api.Parent);
        Assert.DoesNotContain("hide", api.Writes);
    }

    [Fact]
    public void FailedPreparationDoesNotMutateVisibleOrForeignOwnedPopup()
    {
        var api = new Operations { Parent = 4 };
        Assert.False(CocoaPopupConfiguration.Prepare(1, 2, ref api));
        api.Parent = 0;
        api.Visible = true;
        Assert.False(CocoaPopupConfiguration.Prepare(1, 2, ref api));
        Assert.False(CocoaPopupConfiguration.Show(1, 2, ref api, () => Assert.Fail("unowned show")));
        Assert.Empty(api.Writes);
    }

    [Fact]
    public void HiddenPopupGetsActualParentAndPreservesUnrelatedState()
    {
        var api = new Operations { Parent = 3 };
        Assert.True(CocoaPopupConfiguration.Apply(1, 2, ref api));
        Assert.Equal((nint)1, api.Parent);
        Assert.False(api.Hides);
        Assert.False(api.Visible);
        Assert.Equal(new[] { "remove:3", "add:1", "hides:False" }, api.Writes);
        api.Writes.Clear();
        Assert.True(CocoaPopupConfiguration.Apply(1, 2, ref api));
        Assert.Equal(new[] { "hides:False" }, api.Writes); // No detach/re-add.
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public void InvalidAdmissionDoesNotMutate(bool visible, bool identity, bool cycle)
    {
        var api = new Operations { Visible = visible, Identity = identity, Cycle = cycle };
        Assert.False(CocoaPopupConfiguration.Apply(1, 2, ref api));
        Assert.Empty(api.Writes);
    }

    [Fact]
    public void RejectedMutationRestoresPreviousHiddenOwnershipAndFlags()
    {
        var api = new Operations { Parent = 3, RejectAdd = true };
        Assert.False(CocoaPopupConfiguration.Apply(1, 2, ref api));
        Assert.Equal((nint)3, api.Parent);
        Assert.True(api.Hides);
        Assert.Equal(new[] { "remove:3", "add:1", "add:3", "hides:True" }, api.Writes);
    }

    [Fact]
    public void FailedFlagPublicationRestoresParentWithoutClaimingSuccess()
    {
        var api = new Operations { Parent = 3, RejectFlag = true };
        Assert.False(CocoaPopupConfiguration.Apply(1, 2, ref api));
        Assert.Equal((nint)3, api.Parent);
        Assert.True(api.Hides);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void ReentrantHostChangesAreRejectedWithoutOverwritingThem(bool replaceIdentity, bool show, bool reparent)
    {
        var api = new Operations { Parent = 3 };
        api.AfterAdd = () =>
        {
            if (replaceIdentity) api.Identity = false;
            if (show) api.Visible = true;
            if (reparent) api.Parent = 4;
        };
        Assert.False(CocoaPopupConfiguration.Apply(1, 2, ref api));
        Assert.Equal(new[] { "remove:3", "add:1" }, api.Writes);
    }

    [Fact]
    public void CallbackFailurePropagatesAfterRollback()
    {
        var api = new Operations { Parent = 3 };
        api.AfterAdd = () => { api.AfterAdd = null; throw new InvalidOperationException("host callback"); };
        var error = Assert.Throws<InvalidOperationException>(() => CocoaPopupConfiguration.Apply(1, 2, ref api));
        Assert.Equal("host callback", error.Message);
        Assert.Equal((nint)3, api.Parent);
        Assert.True(api.Hides);
    }

    private sealed class Operations : ICocoaPopupOperations
    {
        internal nint Parent;
        internal bool Visible, Cycle, RejectAdd, RejectFlag, ShowOnAdd;
        internal bool Identity = true, Hides = true;
        internal Action? AfterAdd;
        internal List<string> Writes { get; } = new();
        public bool HasCurrentHostIdentity => Identity;
        public bool IsVisible(nint window) => Visible;
        public nint GetParent(nint window) => window == 2 ? Parent : Cycle && window == 1 ? 2 : 0;
        public bool GetHidesOnDeactivate(nint window) => Hides;
        public void SetHidesOnDeactivate(nint window, bool value)
        {
            Writes.Add($"hides:{value}");
            if (!RejectFlag) Hides = value;
        }
        public void RemoveChild(nint owner, nint child) { Writes.Add($"remove:{owner}"); Parent = 0; }
        public void Hide(nint window) { Writes.Add("hide"); Visible = false; Parent = 0; }
        public void AddChild(nint owner, nint child)
        {
            Writes.Add($"add:{owner}");
            if (!RejectAdd || owner != 1) Parent = owner;
            if (ShowOnAdd && Parent == owner) Visible = true;
            AfterAdd?.Invoke();
        }
    }
}
