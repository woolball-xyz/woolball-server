using Domain.Contracts;

namespace Domain.Utilities;

public static class TaskMetricsBuilder
{
    public static TaskMetrics Build(TaskRequest taskRequest, string nodeId)
    {
        var args = taskRequest.PrivateArgs;

        var requestReceived = PrivateArgsHelper.GetTimestamp(args, "request_received");
        var preprocessingStart = PrivateArgsHelper.GetTimestamp(args, "preprocessing_start");
        var preprocessingEnd = PrivateArgsHelper.GetTimestamp(args, "preprocessing_end");
        var distributeStart = PrivateArgsHelper.GetTimestamp(args, "distribute_start");
        var nodeAcquired = PrivateArgsHelper.GetTimestamp(args, "node_acquired");
        var sentToNode = PrivateArgsHelper.GetTimestamp(args, "sent_to_node");
        var responseReceived = PrivateArgsHelper.GetLong(args, "ts_response_received");
        var operatorId = PrivateArgsHelper.GetString(args, "operator_id");
        var postprocessingStart = PrivateArgsHelper.GetTimestamp(args, "postprocessing_start");
        var postprocessingEnd = PrivateArgsHelper.GetTimestamp(args, "postprocessing_end");

        long SafeDiff(long end, long start) => end > 0 && start > 0 ? end - start : 0;

        var queueWaitMs = SafeDiff(preprocessingStart, requestReceived)
                        + SafeDiff(distributeStart, preprocessingEnd)
                        + SafeDiff(nodeAcquired, distributeStart);

        var preprocessingMs = SafeDiff(preprocessingEnd, preprocessingStart);
        var inferenceMs = SafeDiff(responseReceived, sentToNode);
        var postprocessingMs = SafeDiff(postprocessingEnd, postprocessingStart);
        var totalMs = SafeDiff(postprocessingEnd, requestReceived);

        var metrics = new TaskMetrics
        {
            TaskType = taskRequest.Task,
            Pipeline = new PipelineMetrics
            {
                QueueWaitMs = queueWaitMs,
                PreprocessingMs = preprocessingMs,
                InferenceMs = inferenceMs,
                PostprocessingMs = postprocessingMs,
                TotalMs = totalMs,
            },
            Nodes = new List<NodeContribution>
            {
                new()
                {
                    NodeId = nodeId ?? PrivateArgsHelper.GetString(args, "node_id"),
                    InferenceMs = inferenceMs,
                    OperatorId = operatorId,
                }
            }
        };

        return metrics;
    }
}
