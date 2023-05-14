﻿using System.Collections.Specialized;
using Logicality.LittleForker.Infra;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Logicality.LittleForker;

public class ProcessSupervisorTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ILoggerFactory    _loggerFactory;

    public ProcessSupervisorTests(ITestOutputHelper outputHelper)
    {
        _outputHelper  = outputHelper;
        _loggerFactory = new XunitLoggerFactory(outputHelper).LoggerFactory;
    }

    [Fact]
    public async Task Given_invalid_process_path_then_state_should_be_StartError()
    {
        using var supervisor = new ProcessSupervisor(_loggerFactory, ProcessRunType.NonTerminating, "c:/", "invalid.exe");
        var stateIsStartFailed = supervisor.WhenStateIs(ProcessSupervisor.State.StartFailed);
        await supervisor.Start();

        await stateIsStartFailed;
        supervisor.CurrentState.ShouldBe(ProcessSupervisor.State.StartFailed);
        supervisor.OnStartException.ShouldNotBeNull();

        _outputHelper.WriteLine(supervisor.OnStartException.ToString());
    }

    [Fact]
    public async Task Given_invalid_working_directory_then_state_should_be_StartError()
    {
        using var supervisor = new ProcessSupervisor(_loggerFactory, ProcessRunType.NonTerminating, "c:/does_not_exist", "git.exe");
        await supervisor.Start();

        supervisor.CurrentState.ShouldBe(ProcessSupervisor.State.StartFailed);
        supervisor.OnStartException.ShouldNotBeNull();

        _outputHelper.WriteLine(supervisor.OnStartException.ToString());
    }

    [Fact]
    public async Task Given_short_running_exe_then_should_run_to_exit()
    {
        var envVars = new StringDictionary {{"a", "b"}};
        using var supervisor = new ProcessSupervisor(
            _loggerFactory,
            ProcessRunType.SelfTerminating,
            Environment.CurrentDirectory,
            Constants.DotNet,
            Constants.SelfTerminatingProcessPath,
            envVars);
        supervisor.OutputDataReceived += _outputHelper.WriteLine2;
        var whenStateIsExited          = supervisor.WhenStateIs(ProcessSupervisor.State.ExitedSuccessfully);
        var whenStateIsExitedWithError = supervisor.WhenStateIs(ProcessSupervisor.State.ExitedWithError);

        await supervisor.Start();

        var task = await Task.WhenAny(whenStateIsExited, whenStateIsExitedWithError);

        task.ShouldBe(whenStateIsExited);
        supervisor.CurrentState.ShouldBe(ProcessSupervisor.State.ExitedSuccessfully);
        supervisor.OnStartException.ShouldBeNull();
        supervisor.ProcessInfo!.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Given_non_terminating_process_then_should_exit_when_stopped()
    {
        using var supervisor = new ProcessSupervisor(
            _loggerFactory,
            ProcessRunType.NonTerminating,
            Environment.CurrentDirectory,
            Constants.DotNet,
            Constants.NonTerminatingProcessPath);
        supervisor.OutputDataReceived += data => _outputHelper.WriteLine2($"Process: {data}");
        var running = supervisor.WhenStateIs(ProcessSupervisor.State.Running);
        await supervisor.Start();

        supervisor.CurrentState.ShouldBe(ProcessSupervisor.State.Running);
        await running;

        await supervisor.Stop(TimeSpan.FromSeconds(5));

        supervisor.CurrentState.ShouldBe(ProcessSupervisor.State.ExitedSuccessfully);
        supervisor.OnStartException.ShouldBeNull();
        supervisor.ProcessInfo.ExitCode.ShouldBe(0);
    }
        
    [Fact]
    public async Task Can_restart_a_stopped_short_running_process()
    {
        var supervisor = new ProcessSupervisor(
            _loggerFactory,
            ProcessRunType.SelfTerminating,
            Environment.CurrentDirectory,
            Constants.DotNet,
            Constants.SelfTerminatingProcessPath);
        supervisor.OutputDataReceived += _outputHelper.WriteLine2;
        var stateIsStopped = supervisor.WhenStateIs(ProcessSupervisor.State.ExitedSuccessfully);
        await supervisor.Start();
        await stateIsStopped;

        await supervisor.Start();
        await stateIsStopped;
    }

    [Fact]
    public async Task Can_restart_a_stopped_long_running_process()
    {
        var supervisor = new ProcessSupervisor(
            _loggerFactory,
            ProcessRunType.NonTerminating,
            Environment.CurrentDirectory,
            Constants.DotNet,
            Constants.NonTerminatingProcessPath);
        supervisor.OutputDataReceived += _outputHelper.WriteLine2;
        var exitedKilled = supervisor.WhenStateIs(ProcessSupervisor.State.ExitedKilled);
        await supervisor.Start();
        await supervisor.Stop();
        await exitedKilled.TimeoutAfter(TimeSpan.FromSeconds(5));

        // Restart
        var exitedSuccessfully = supervisor.WhenStateIs(ProcessSupervisor.State.ExitedSuccessfully);
        await supervisor.Start();
        await supervisor.Stop(TimeSpan.FromSeconds(2));
        await exitedSuccessfully;
    }

    [Fact]
    public async Task When_stop_a_non_terminating_process_without_a_timeout_then_should_exit_killed()
    {
        using var supervisor = new ProcessSupervisor(
            _loggerFactory,
            ProcessRunType.NonTerminating,
            Environment.CurrentDirectory,
            Constants.DotNet,
            Constants.NonTerminatingProcessPath);
        supervisor.OutputDataReceived += _outputHelper.WriteLine2;
        var stateIsStopped = supervisor.WhenStateIs(ProcessSupervisor.State.ExitedKilled);
        await supervisor.Start();
        await supervisor.Stop(); // No timeout so will just kill the process
        await stateIsStopped.TimeoutAfter(TimeSpan.FromSeconds(2));

        _outputHelper.WriteLine($"Exit code {supervisor.ProcessInfo!.ExitCode}");
    }
        
    [Fact]
    public async Task When_stop_a_non_terminating_process_that_does_not_shutdown_within_timeout_then_should_exit_killed()
    {
        using var supervisor = new ProcessSupervisor(
            _loggerFactory,
            ProcessRunType.NonTerminating,
            Environment.CurrentDirectory,
            Constants.DotNet,
            $"{Constants.NonTerminatingProcessPath} --ignore-shutdown-signal=true");
        supervisor.OutputDataReceived += _outputHelper.WriteLine2;
        var stateIsKilled = supervisor.WhenStateIs(ProcessSupervisor.State.ExitedKilled);
        await supervisor.Start();
        await supervisor.Stop(TimeSpan.FromSeconds(2));
        await stateIsKilled.TimeoutAfter(TimeSpan.FromSeconds(5));

        _outputHelper.WriteLine($"Exit code {supervisor.ProcessInfo!.ExitCode}");
    }

    [Fact]
    public async Task When_stop_a_non_terminating_process_with_non_zero_then_should_exit_error()
    {
        using var supervisor = new ProcessSupervisor(
            _loggerFactory,
            ProcessRunType.NonTerminating,
            Environment.CurrentDirectory,
            Constants.DotNet,
            $"{Constants.NonTerminatingProcessPath} --exit-with-non-zero=true");
        supervisor.OutputDataReceived += _outputHelper.WriteLine2;
        var stateExitWithError = supervisor.WhenStateIs(ProcessSupervisor.State.ExitedWithError);
        await supervisor.Start();
        await supervisor.Stop(TimeSpan.FromSeconds(5));
        await stateExitWithError.TimeoutAfter(TimeSpan.FromSeconds(5));
        supervisor.ProcessInfo!.ExitCode.ShouldNotBe(0);

        _outputHelper.WriteLine($"Exit code {supervisor.ProcessInfo.ExitCode}");
    }

    [Fact]
    public async Task Can_attempt_to_restart_a_failed_short_running_process()
    {
        using var supervisor = new ProcessSupervisor(
            _loggerFactory,
            ProcessRunType.NonTerminating,
            Environment.CurrentDirectory,
            "invalid.exe");
        await supervisor.Start();

        supervisor.CurrentState.ShouldBe(ProcessSupervisor.State.StartFailed);
        supervisor.OnStartException.ShouldNotBeNull();

        await supervisor.Start();

        supervisor.CurrentState.ShouldBe(ProcessSupervisor.State.StartFailed);
        supervisor.OnStartException.ShouldNotBeNull();
    }

    [Fact]
    public void WriteDotGraph()
    {
        using var processController = new ProcessSupervisor(
            _loggerFactory, 
            ProcessRunType.NonTerminating, 
            Environment.CurrentDirectory,
            "invalid.exe");
        _outputHelper.WriteLine(processController.GetDotGraph());
    }
}