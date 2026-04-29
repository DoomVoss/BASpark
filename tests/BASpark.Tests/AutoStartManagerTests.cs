namespace BASpark.Tests;

public class AutoStartManagerTests
{
    [Fact]
    public void CreatePlan_UsesRegistryForNormalAutoStart()
    {
        AutoStartPlan plan = AutoStartManager.CreatePlan(autoStart: true, runAsAdmin: false);

        Assert.True(plan.RegistryRunEnabled);
        Assert.False(plan.ScheduledTaskEnabled);
    }

    [Fact]
    public void CreatePlan_UsesScheduledTaskForElevatedAutoStart()
    {
        AutoStartPlan plan = AutoStartManager.CreatePlan(autoStart: true, runAsAdmin: true);

        Assert.False(plan.RegistryRunEnabled);
        Assert.True(plan.ScheduledTaskEnabled);
    }

    [Fact]
    public void CreatePlan_DisablesBothStartupMechanismsWhenAutoStartIsOff()
    {
        AutoStartPlan plan = AutoStartManager.CreatePlan(autoStart: false, runAsAdmin: true);

        Assert.False(plan.RegistryRunEnabled);
        Assert.False(plan.ScheduledTaskEnabled);
    }

    [Fact]
    public void BuildRunCommand_AddsAutostartArgumentToQuotedExecutablePath()
    {
        string command = AutoStartManager.BuildRunCommand(@"C:\Program Files\BASpark\BASpark.exe");

        Assert.Equal(@"""C:\Program Files\BASpark\BASpark.exe"" --autostart", command);
    }

    [Fact]
    public void ResolveExecutablePath_PrefersProcessExeOverAssemblyDll()
    {
        string? resolved = AutoStartManager.ResolveExecutablePath(
            processPath: @"C:\Apps\BASpark\BASpark.exe",
            mainModulePath: @"C:\Apps\BASpark\BASpark.exe",
            assemblyLocation: @"C:\Apps\BASpark\BASpark.dll",
            baseDirectory: @"C:\Apps\BASpark");

        Assert.Equal(@"C:\Apps\BASpark\BASpark.exe", resolved);
    }
}
