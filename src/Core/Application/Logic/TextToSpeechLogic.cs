using System.Collections.Concurrent;
using Domain.Contracts;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Application.Logic;

public sealed class TextToSpeechLogic : ITextToSpeechLogic
{
    private readonly IConnectionMultiplexer _redis;
    private static readonly ConcurrentDictionary<string, List<TTSResponse>> _buffers = new();

    public TextToSpeechLogic(IConnectionMultiplexer redis) => _redis = redis;

    public async Task ProcessTaskResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        try 
        {
            var requestId = taskResponse.Data.RequestId;
            Console.WriteLine($"[TextToSpeechLogic] Processing TTS response for request {requestId}");
            
            // Verificar se há um ID de parent, indicando que é parte de um texto grande dividido
            if (taskRequest.PrivateArgs.TryGetValue("parent", out var parentValue) && 
                parentValue is string parentId)
            {
                // Processar como parte de um conjunto de chunks
                Console.WriteLine($"[TextToSpeechLogic] Processing as chunk for parent {parentId}");
                await ProcessChunkResponseAsync(taskResponse, taskRequest, parentId);
            }
            else
            {
                // Processar como única resposta
                Console.WriteLine($"[TextToSpeechLogic] Processing as single response");
                await ProcessSingleResponseAsync(taskResponse, taskRequest);
            }
            
            // Indica que o processamento foi concluído com sucesso
            Console.WriteLine($"[TextToSpeechLogic] Successfully completed processing for request {requestId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TextToSpeechLogic] Error processing task response: {ex.Message}");
            Console.WriteLine($"[TextToSpeechLogic] Stack trace: {ex.StackTrace}");
            
            // Try to send an error response
            try 
            {
                var subscriber = _redis.GetSubscriber();
                var resultQueueName = $"result_queue_{taskRequest.Id}";
                
                // Verificar se estamos trabalhando com chunks ou resposta única
                bool isChunkedResponse = taskRequest.PrivateArgs.ContainsKey("parent");
                
                if (isChunkedResponse)
                {
                    // Caso de chunks - enviar uma lista vazia
                    await subscriber.PublishAsync(
                        resultQueueName,
                        JsonSerializer.Serialize(new List<TTSResponse>())
                    );
                }
                else
                {
                    // Caso de resposta única - enviar um TTSResponse vazio
                    await subscriber.PublishAsync(
                        resultQueueName,
                        JsonSerializer.Serialize(new TTSResponse {
                            AudioBase64 = "",
                            Format = "wav",
                            SampleRate = 16000
                        })
                    );
                }
                
                Console.WriteLine($"[TextToSpeechLogic] Sent error response to prevent retries for task {taskRequest.Id}");
            }
            catch (Exception innerEx)
            {
                // If this also fails, just log it
                Console.WriteLine($"[TextToSpeechLogic] Failed to send error response: {innerEx.Message}");
            }
            
            // Não relançar a exceção para evitar retentativas
        }
    }

    private async Task ProcessSingleResponseAsync(TaskResponse taskResponse, TaskRequest taskRequest)
    {
        var subscriber = _redis.GetSubscriber();
        var resultQueueName = $"result_queue_{taskRequest.Id}";
        
        try
        {
            // Extrair o TTSResponse da resposta
            TTSResponse ttsResponse = ExtractTTSResponse(taskResponse.Data.Response);
            
            if (string.IsNullOrEmpty(ttsResponse.AudioBase64))
            {
                Console.WriteLine($"[TextToSpeechLogic] Warning: Empty audio data in response");
                // Criar uma resposta genérica para evitar falha
                ttsResponse = new TTSResponse
                {
                    AudioBase64 = "", // Audio vazio, mas válido estruturalmente
                    Format = "wav",
                    SampleRate = 16000
                };
                Console.WriteLine($"[TextToSpeechLogic] Created empty but valid response to prevent retries");
            }
            else 
            {
                Console.WriteLine($"[TextToSpeechLogic] Audio data available in response");
            }
            
            // Publicar APENAS o TTSResponse diretamente (não o TextToSpeechResponseData)
            string serializedResponse = JsonSerializer.Serialize(ttsResponse);
            Console.WriteLine($"[TextToSpeechLogic] Serialized response (first 100 chars): {serializedResponse.Substring(0, Math.Min(100, serializedResponse.Length))}...");
            
            await subscriber.PublishAsync(
                resultQueueName,
                serializedResponse
            );
            
            Console.WriteLine($"[TextToSpeechLogic] Published TTS response to {resultQueueName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TextToSpeechLogic] Error in ProcessSingleResponseAsync: {ex.Message}");
            Console.WriteLine($"[TextToSpeechLogic] Exception type: {ex.GetType().FullName}");
            
            // Enviar uma resposta de erro para evitar retentativas
            try
            {
                // Enviar apenas um TTSResponse com erro
                var emptyResponse = new TTSResponse
                {
                    AudioBase64 = "",
                    Format = "wav",
                    SampleRate = 16000
                };
                
                await subscriber.PublishAsync(
                    resultQueueName,
                    JsonSerializer.Serialize(emptyResponse)
                );
                
                Console.WriteLine($"[TextToSpeechLogic] Sent error response to prevent retries");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"[TextToSpeechLogic] Failed to send error response: {innerEx.Message}");
            }
        }
    }

    private async Task ProcessChunkResponseAsync(
        TaskResponse taskResponse, 
        TaskRequest taskRequest, 
        string parentId)
    {
        var subscriber = _redis.GetSubscriber();
        var resultQueueName = $"result_queue_{parentId}";
        
        try
        {
            // Coletamos todos os chunks antes de enviar o resultado completo
            if (!_buffers.TryGetValue(parentId, out var buffer))
            {
                buffer = new List<TTSResponse>();
                _buffers[parentId] = buffer;
            }
            
            // Extrair o número da ordem atual
            if (!taskRequest.PrivateArgs.TryGetValue("order", out var orderObj) || 
                !int.TryParse(orderObj.ToString(), out int order))
            {
                Console.WriteLine($"[TextToSpeechLogic] Missing order value, using default order 1");
                order = 1;
            }
            
            bool isLast = false;
            if (taskRequest.PrivateArgs.TryGetValue("last", out var lastObj) &&
                bool.TryParse(lastObj.ToString(), out isLast))
            {
                // Successfully parsed isLast
            }
            else
            {
                // Se não foi possível extrair 'isLast', verifica se é o último chunk com base no retry_count
                // Um valor alto de retry_count sugere que estamos perto do fim do processamento
                isLast = taskRequest.PrivateArgs.TryGetValue("retry_count", out var retryObj) && 
                        int.TryParse(retryObj?.ToString() ?? "0", out var retryCount) && 
                        retryCount >= 2;
            }
            
            Console.WriteLine($"[TextToSpeechLogic] Processing chunk {order} (isLast: {isLast}) for parent {parentId}");
            
            // Extrair o TTSResponse da resposta
            TTSResponse ttsResponse = ExtractTTSResponse(taskResponse.Data.Response);
            
            if (string.IsNullOrEmpty(ttsResponse.AudioBase64))
            {
                Console.WriteLine($"[TextToSpeechLogic] Warning: Empty audio data in chunk {order}");
                // Criar uma resposta vazia mas estruturalmente válida
                ttsResponse = new TTSResponse
                {
                    AudioBase64 = "",
                    Format = "wav",
                    SampleRate = 16000
                };
            }
            
            // Adicionar resultado ao buffer na posição correta
            while (buffer.Count < order)
            {
                buffer.Add(new TTSResponse());
            }
            
            if (buffer.Count == order)
                buffer.Add(ttsResponse);
            else
                buffer[order - 1] = ttsResponse;
            
            // Verificar se temos todos os chunks ou se este é o último chunk forçadamente
            if (isLast)
            {
                Console.WriteLine($"[TextToSpeechLogic] Last chunk received, sending complete response for {parentId}");
                
                // Verificar se algum chunk está vazio (exceto por espaços em branco)
                bool hasEmptyChunk = buffer.Any(chunk => string.IsNullOrWhiteSpace(chunk.AudioBase64));
                if (hasEmptyChunk)
                {
                    Console.WriteLine($"[TextToSpeechLogic] Warning: Some chunks have empty audio data");
                }
                
                // Publicar APENAS a lista de TTSResponse, não o wrapper completo
                string serializedResponse = JsonSerializer.Serialize(buffer);
                Console.WriteLine($"[TextToSpeechLogic] Serialized complete response (first 100 chars): {serializedResponse.Substring(0, Math.Min(100, serializedResponse.Length))}...");
                
                await subscriber.PublishAsync(
                    resultQueueName,
                    serializedResponse
                );
                
                // Limpar o buffer
                _buffers.TryRemove(parentId, out _);
                
                Console.WriteLine($"[TextToSpeechLogic] Successfully published final chunk response to {resultQueueName}");
            }
            else
            {
                Console.WriteLine($"[TextToSpeechLogic] Chunk {order} processed and stored, waiting for more chunks");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TextToSpeechLogic] Error in ProcessChunkResponseAsync: {ex.Message}");
            Console.WriteLine($"[TextToSpeechLogic] Exception type: {ex.GetType().FullName}");
            
            // Enviar uma resposta de erro para evitar retentativas
            try
            {
                // Forçar envio de uma resposta completa para evitar que fique esperando infinitamente
                // por chunks que nunca chegarão - enviar APENAS a lista de TTSResponse
                var emptyResponseList = new List<TTSResponse> { 
                    new TTSResponse {
                        AudioBase64 = "", 
                        Format = "wav",
                        SampleRate = 16000
                    }
                };
                
                await subscriber.PublishAsync(
                    resultQueueName,
                    JsonSerializer.Serialize(emptyResponseList)
                );
                
                // Limpar o buffer para este parentId
                _buffers.TryRemove(parentId, out _);
                
                Console.WriteLine($"[TextToSpeechLogic] Sent error response to prevent retries for parent {parentId}");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"[TextToSpeechLogic] Failed to send error response: {innerEx.Message}");
            }
        }
    }
    
    // Método para extrair TTSResponse a partir do objeto Response retornado
    private TTSResponse ExtractTTSResponse(object responseObj)
    {
        try
        {
            Console.WriteLine($"[TextToSpeechLogic] ExtractTTSResponse from type: {responseObj?.GetType().FullName ?? "null"}");
            
            // Caso 1: Já é um TTSResponse
            if (responseObj is TTSResponse ttsResponse)
            {
                Console.WriteLine("[TextToSpeechLogic] Response is already TTSResponse");
                return ttsResponse;
            }
            
            
            // Caso 3: É um JsonElement
            if (responseObj is JsonElement jsonElement)
            {
                Console.WriteLine("[TextToSpeechLogic] Response is JsonElement");
                
                // Verificar se tem uma propriedade "audio" diretamente
                if (jsonElement.TryGetProperty("audio", out var audioProperty))
                {
                    return new TTSResponse
                    {
                        AudioBase64 = audioProperty.GetString() ?? string.Empty,
                        Format = jsonElement.TryGetProperty("format", out var formatProp) 
                            ? formatProp.GetString() ?? "wav" : "wav",
                        SampleRate = jsonElement.TryGetProperty("sample_rate", out var sampleRateProp) 
                            ? sampleRateProp.GetInt32() : 16000
                    };
                }
                
                // Se tem uma propriedade "response", tentar dentro dela
                if (jsonElement.TryGetProperty("response", out var responseProp))
                {
                    if (responseProp.ValueKind == JsonValueKind.Object && 
                        responseProp.TryGetProperty("audio", out var innerAudioProp))
                    {
                        return new TTSResponse
                        {
                            AudioBase64 = innerAudioProp.GetString() ?? string.Empty,
                            Format = responseProp.TryGetProperty("format", out var formatProp) 
                                ? formatProp.GetString() ?? "wav" : "wav",
                            SampleRate = responseProp.TryGetProperty("sample_rate", out var sampleRateProp) 
                                ? sampleRateProp.GetInt32() : 16000
                        };
                    }
                }
            }
            
            // Caso 4: Tenta serializar e deserializar o objeto
            string json = JsonSerializer.Serialize(responseObj);
            Console.WriteLine($"[TextToSpeechLogic] Attempting to extract from JSON: {json.Substring(0, Math.Min(100, json.Length))}...");
            
            try
            {
                // Tenta deserializar diretamente para TTSResponse
                var result = JsonSerializer.Deserialize<TTSResponse>(json);
                if (result != null && !string.IsNullOrEmpty(result.AudioBase64))
                {
                    Console.WriteLine("[TextToSpeechLogic] Successfully extracted TTSResponse from JSON");
                    return result;
                }
                
                // Tenta extrair de um objeto que contenha propriedades aninhadas
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                // Procura por qualquer propriedade que possa conter o áudio
                string audioBase64 = FindAudioProperty(root);
                if (!string.IsNullOrEmpty(audioBase64))
                {
                    Console.WriteLine("[TextToSpeechLogic] Found audio property in JSON");
                    return new TTSResponse
                    {
                        AudioBase64 = audioBase64,
                        Format = "wav",
                        SampleRate = 16000
                    };
                }
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"[TextToSpeechLogic] Error parsing JSON: {ex.Message}");
            }
            
            // Caso falhe, retorna um TTSResponse vazio
            Console.WriteLine($"[TextToSpeechLogic] Could not extract TTSResponse");
            return new TTSResponse();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TextToSpeechLogic] Error extracting TTSResponse: {ex.Message}");
            return new TTSResponse();
        }
    }
    
    // Método recursivo para encontrar uma propriedade de áudio em qualquer nível do JSON
    private string FindAudioProperty(JsonElement element, int depth = 0)
    {
        if (depth > 3) return string.Empty; // Limitar a profundidade da recursão
        
        // Verificar se este elemento tem uma propriedade "audio"
        if (element.TryGetProperty("audio", out var audioProp) && 
            audioProp.ValueKind == JsonValueKind.String)
        {
            return audioProp.GetString() ?? string.Empty;
        }
        
        // Se for um objeto, verificar todas as propriedades
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object || 
                    prop.Value.ValueKind == JsonValueKind.Array)
                {
                    string result = FindAudioProperty(prop.Value, depth + 1);
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                }
            }
        }
        
        // Se for um array, verificar todos os elementos
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                string result = FindAudioProperty(item, depth + 1);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
        }
        
        return string.Empty;
    }
} 