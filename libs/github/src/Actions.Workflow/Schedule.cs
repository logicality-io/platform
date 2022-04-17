﻿using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Logicality.GitHub.Actions.Workflow;

public class Schedule: Trigger
{
    private readonly string[] _crons;

    public Schedule(
        string   eventName,
        string[] crons,
        On       @on,
        Workflow workflow)
        : base(eventName, @on, workflow)
    {
        _crons = crons;
    }

    internal override void Build(YamlMappingNode parent, SequenceStyle sequenceStyle)
    {
        var sequence = new YamlSequenceNode
        {
            Style = sequenceStyle
        };
        foreach (var cron in _crons)
        {
            sequence.Add(new YamlMappingNode("cron", new YamlScalarNode(cron){ Style = ScalarStyle.SingleQuoted}));
        }

        parent.Add("schedule", sequence);
    }
}