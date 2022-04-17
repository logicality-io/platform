﻿using Logicality.GitHub.Actions.Workflow;

void WriteWorkflow(Workflow workflow, string fileName)
{
    var path = "../workflows";
    var yaml     = workflow.GetYaml();
    var filePath = $"{path}/{fileName}.yml";

    File.WriteAllText(filePath, yaml);

    Console.WriteLine($"Wrote workflow to {filePath}");
}

void GenerateWorkflowsForLibs()
{
    var libs = new[]
    {
        "aspnet-core",
        "bullseye",
        "configuration",
        "github",
        "hosting",
        "lambda",
        "pulumi",
        "system-extensions",
        "testing"
    };

    foreach (var lib in libs)
    {
        var workflow = new Workflow($"{lib}-ci");

        var paths = new[] { $".github/workflows/{lib}-**", $"libs/{lib}**", "build/**" };

        workflow.On
            .PullRequest()
            .Paths(paths);

        workflow.On
            .Push()
            .Branches("main")
            .Paths(paths)
            .Tags($"{lib}-**");

        var buildJob = workflow
            .Job("build")
            .RunsOn("ubuntu-latest")
            .Env(new Dictionary<string, string>
            {
                { "GITHUB_TOKEN", "${{secrets.GITHUB_TOKEN}}" }
            });

        buildJob.CheckoutStep();

        buildJob.LogIntoGitHubContainerRegistryStep();

        buildJob.PrintEnvironmentStep();

        buildJob.Step()
            .Name("Test")
            .Run($"./build.ps1 {lib}-test")
            .ShellPowerShell();

        buildJob.Step()
            .Name("Pack")
            .Run($"./build.ps1 {lib}-pack")
            .ShellPowerShell();

        buildJob.Step()
            .Name("Push")
            .If("github.event_name == 'push'")
            .Run("./build.ps1 push")
            .ShellPowerShell();

        buildJob.UploadArtifacts();

        var fileName = $"{lib}-ci";

        WriteWorkflow(workflow, fileName);
    }
}

void GenerateCodeAnalysisWorkflow()
{
    var workflow = new Workflow("CodeQL");

    workflow.On
        .Push()
        .Branches("main");
    workflow.On
        .PullRequest()
        .Branches("main");
    workflow.On
        .Schedule("'39 8 * * 1'");

    var job = workflow.Job("analyze")
        .RunsOn("ubuntu-latest")
        .Permissions(
            actions: Permission.Read,
            contents: Permission.Read,
            securityEvents: Permission.Write);

    job.Step("");
}

GenerateWorkflowsForLibs();