using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.ClientModel;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenCvSharp;
using System.Net.Http;
using System.Threading.Tasks;
using System; 
using System.Collections.Generic; 
using Azure.Communication.Email;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Read configuration values
string openAIKey = configuration["OpenAI:Key"] ??
                   throw new ArgumentException("Missing OpenAI:Key");
string openAIEndpoint = configuration["OpenAI:Endpoint"] ??
                        throw new ArgumentException("Missing OpenAI:Endpoint");
string engine = configuration["OpenAI:DeploymentName"] ??
                throw new ArgumentException("Missing OpenAI:DeploymentName");

string imageOpenAIKey = configuration["OpenAI:imageKey"] ??
                   throw new ArgumentException("Missing OpenAI:imageKey");
string imageOpenAIEndpoint = configuration["OpenAI:imageEndpoint"] ??
                        throw new ArgumentException("Missing OpenAI:imageEndpoint");
string imageEngine = configuration["OpenAI:ImageDeploymentName"] ??
                     throw new ArgumentException("Missing OpenAI:ImageDeploymentName");

string speechKey = configuration["Speech:Key"] ??
                   throw new ArgumentException("Missing Speech:Key");
string speechRegion = configuration["Speech:Region"] ??
                      throw new ArgumentException("Missing Speech:Region");
string communicationServiceConnectionString = configuration["CommunicationService:ConnectionString"] ??
                                            throw new ArgumentException("Missing CommunicationService:ConnectionString");
string communicationServiceDomain = configuration["CommunicationService:Domain"] ??
                                    throw new ArgumentException("Missing CommunicationService:Domain");

// Create a chat client
AzureOpenAIClient azureClient = new(
    new Uri(openAIEndpoint),
    new AzureKeyCredential(openAIKey));

ChatClient chatClient = azureClient.GetChatClient(engine);

// Create an image client
AzureOpenAIClient azureImageClient = new(
    new Uri(imageOpenAIEndpoint),
    new AzureKeyCredential(imageOpenAIKey));
ChatClient imageChatClient = azureImageClient.GetChatClient(imageEngine);

async static Task<string> GetCurrentWeather(string latitude, string longitude, string unit = "celsius")
{
    string url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,wind_speed_10m&hourly=temperature_2m,relative_humidity_2m,wind_speed_10m";
    using (HttpClient client = new HttpClient())
    {
        HttpResponseMessage response = client.GetAsync(url).Result;
        response.EnsureSuccessStatusCode();
        string responseBody = response.Content.ReadAsStringAsync().Result;
        // Parse the JSON response
        JsonDocument jsonDoc = JsonDocument.Parse(responseBody);
        JsonElement current = jsonDoc.RootElement.GetProperty("current");
        double temperature = current.GetProperty("temperature_2m").GetDouble();
        double windSpeed = current.GetProperty("wind_speed_10m").GetDouble();
        string temperatureUnit = unit.ToLower() == "fahrenheit" ? "°F" : "°C";
        return $"Current temperature: {temperature}{temperatureUnit}, Wind speed: {windSpeed} m/s at latitude {latitude} and longitude {longitude}";
    }
}

async static Task<string> verifyIdentity(string lastName, string firstCarMake)
{
    string tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp");
    string filePath = Path.Combine(tmpDir, "sample_verification.json");
    string jsonContent = File.ReadAllText(filePath);
    
    // Parse the JSON content
    JsonDocument jsonDoc = JsonDocument.Parse(jsonContent);
    JsonElement users = jsonDoc.RootElement.GetProperty("users");
    
    foreach (JsonElement user in users.EnumerateArray())
    {
        string userLastName = user.GetProperty("lastName").GetString();
        string userFirstCarMake = user.GetProperty("firstCarMake").GetString();
        
        if (userLastName.ToLower() == lastName.ToLower() && userFirstCarMake.ToLower() == firstCarMake.ToLower())
        {
            return $"Verification successful.";
        }
    }
    
    return "Verification failed. User not found.";
}

async static Task<string> checkWellnessBalance(string lastName)
{
    string tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp");
    string filePath = Path.Combine(tmpDir, "sample_verification.json");
    string jsonContent = File.ReadAllText(filePath);
    
    // Parse the JSON content
    JsonDocument jsonDoc = JsonDocument.Parse(jsonContent);
    JsonElement users = jsonDoc.RootElement.GetProperty("users");
    
    foreach (JsonElement user in users.EnumerateArray())
    {
        string userLastName = user.GetProperty("lastName").GetString();
        
        if (userLastName.ToLower() == lastName.ToLower())
        {
            double wellnessPool = user.GetProperty("wellnessPool").GetDouble();
            return $"Wellness pool amount: ${wellnessPool}";
        }
    }
    
    return "User not found.";
}

async Task<string> ReadReceipt()
{
        // Call the camera API here.
        string imagePath = CaptureImage();

        // Call OpenAI API to read the image.
        ChatMessageContentPart imagePart;
        using var stream = File.OpenRead(imagePath);
        var imageData = BinaryData.FromStream(stream);

        imagePart = ChatMessageContentPart.CreateImageMessageContentPart(
            imageData, "image/jpg", ImageChatMessageContentPartDetail.High);

        ChatMessage[] messages =
        [
            new SystemChatMessage("You are a helpful assistant that can read text from receipts and invoices. Please extract key information and share as a succinct, easy to understand natural language summary."),
            new UserChatMessage(imagePart, ChatMessageContentPart.CreateTextMessageContentPart("Read this receipt and summarize the following information: name of store, total number of items purchased, total amount spent, and date of transaction. Generate this information as a simple summary that can be trascribed back to the user or put in an email summary.")),
        ];

        var imageAnalysisResponseText = new StringBuilder();

        ResultCollection<StreamingChatCompletionUpdate> completionUpdates = imageChatClient.CompleteChatStreaming(messages);

        foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
        {
            foreach (ChatMessageContentPart contentPart in completionUpdate.ContentUpdate)
            {
                imageAnalysisResponseText.Append(contentPart.Text);
            }
        }

        return imageAnalysisResponseText.ToString();
}

static string CaptureImage()
{   
    // Ensure the tmp directory exists
    string tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tmp");
    if (!Directory.Exists(tmpDir))
    {
        Directory.CreateDirectory(tmpDir);
    }

    // Define the image path
    string imagePath = Path.Combine(tmpDir, "image.jpg");

    // Capture image from webcam
    using (var capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW))
    {
        if (!capture.IsOpened())
        {
            throw new ApplicationException("No video devices found.");
        }

        // Set the resolution to match your webcam's resolution
        capture.Set(VideoCaptureProperties.FrameWidth, 1920); // Set width
        capture.Set(VideoCaptureProperties.FrameHeight, 1080); // Set height

        using (var frame = new Mat())
        {
            capture.Read(frame);
            if (frame.Empty())
            {
                throw new ApplicationException("Failed to capture image.");
            }

            // Adjust brightness and contrast
            Mat adjustedFrame = new Mat();
            double alpha = 1.2; // Contrast control (1.0-3.0)
            int beta = 10;     // Brightness control (0-100)
            frame.ConvertTo(adjustedFrame, -1, alpha, beta);

            // Save the captured image
            Cv2.ImWrite(imagePath, adjustedFrame);
        }
    }
    Console.WriteLine($"Image captured and saved.");
    return imagePath;
}

void SendEmail(string emailSubject, string emailBody)
{
    // Connection string for Azure Communication Service
    string connectionString = communicationServiceConnectionString;
    
    // Create an EmailClient instance
    var emailClient = new EmailClient(connectionString);
    // Send the email
    EmailSendOperation emailSendOperation = emailClient.Send(
        WaitUntil.Completed,
        senderAddress: communicationServiceDomain,
        recipientAddress: "jawadamin@microsoft.com",
        subject: $"{emailSubject}",
        htmlContent: $"<html><h3>{emailBody}</h3></html>",
        plainTextContent: emailBody);

    Console.WriteLine("Email sent successfully!");
}

ChatTool getCurrentWeatherTool = ChatTool.CreateFunctionTool(
    functionName: nameof(GetCurrentWeather),
    functionDescription: "Get the current weather in a given latitude and longitude.",
    functionParameters: BinaryData.FromString("""
    {
        "type": "object",
        "properties": {
            "latitude": {
                "type": "string",
                "description": "The latitude of the location to get the weather for. Infer this from the user specified location."
            },
            "longitude": {
                "type": "string",
                "description": "The longitude of the location to get the weather for. Infer this from the user specified location."
            },
            "unit": {
                "type": "string",
                "enum": [ "celsius", "fahrenheit" ],
                "description": "The temperature unit to use. Infer this from the specified location."
            }
        },
        "required": [ "latitude", "longitude" ]
    }
    """)
);

ChatTool verifyIdentityTool = ChatTool.CreateFunctionTool(
    functionName: nameof(verifyIdentity),
    functionDescription: "Verify the identity of a user based on their last name and the make of their first car.",
    functionParameters: BinaryData.FromString("""
    {
        "type": "object",
        "properties": {
            "lastName": {
                "type": "string",
                "description": "The last name of the user to verify."
            },
            "firstCarMake": {
                "type": "string",
                "description": "The make of the user's first car."
            }
        },
        "required": [ "lastName", "firstCarMake" ]
    }
    """)
);

ChatTool checkWellnessBalanceTool = ChatTool.CreateFunctionTool(
    functionName: nameof(checkWellnessBalance),
    functionDescription: "Check the wellness balance of a user based on their last name.",
    functionParameters: BinaryData.FromString("""
    {
        "type": "object",
        "properties": {
            "lastName": {
                "type": "string",
                "description": "The last name of the user to verify."
            }
        },
        "required": [ "lastName" ]
    }
    """)
);

ChatTool readReceiptTool = ChatTool.CreateFunctionTool(
    functionName: nameof(ReadReceipt),
    functionDescription: "Use the webcam of the computer to capture an image of a receipt the user is sharing, use OCR to read the text, and return the key details from the receipt."
);

ChatTool sendEmailTool = ChatTool.CreateFunctionTool(
    functionName: nameof(SendEmail),
    functionDescription: "Send an email to the user with the summary of the receipt.",
    functionParameters: BinaryData.FromString("""
    {
        "type": "object",
        "properties": {
            "emailSubject": {
                "type": "string",
                "description": "The subject of the email."
            },
            "emailBody": {
                "type": "string",
                "description": "The body of the email."
            }
        },
        "required": [ "emailSubject", "emailBody" ]
    }
    """)
);


// Each tool call must be resolved, like in the non-streaming case
async Task<string> GetToolCallOutput(ChatToolCall toolCall)
{
    if (toolCall.FunctionName == getCurrentWeatherTool.FunctionName)
    {
        // Validate arguments before using them; it's not always guaranteed to be valid JSON!
        try
        {
            string latitude = "122.3321";
            string longitude = "47.6062";
            string unit = "celsius";
            
            using JsonDocument argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
            if (!argumentsDocument.RootElement.TryGetProperty("latitude", out JsonElement latitudeElement) ||
                !argumentsDocument.RootElement.TryGetProperty("longitude", out JsonElement longitudeElement))
            {
                // Handle missing required "latitude" or "longitude" argument with fixed location
                return await GetCurrentWeather(latitude,longitude, unit);
            }
            else
            {
                latitude = latitudeElement.GetString();
                longitude = longitudeElement.GetString();                
                if (argumentsDocument.RootElement.TryGetProperty("unit", out JsonElement unitElement))
                {
                    return await GetCurrentWeather(latitude, longitude, unitElement.GetString());
                }
                else
                {
                    return await GetCurrentWeather(latitude, longitude);
                }
            }
        }
        catch (JsonException)
        {
            // Handle the JsonException (bad arguments) here
        }
    }
    else if (toolCall.FunctionName == verifyIdentityTool.FunctionName)
    {
        // Validate arguments before using them; it's not always guaranteed to be valid JSON!
        try
        {
            string lastName = "";
            string firstCarMake = "";
            
            using JsonDocument argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
            if (!argumentsDocument.RootElement.TryGetProperty("lastName", out JsonElement lastNameElement) ||
                !argumentsDocument.RootElement.TryGetProperty("firstCarMake", out JsonElement firstCarMakeElement))
            {
                return "Invalid arguments for verifyIdentity.";
            }

            lastName = lastNameElement.GetString();
            firstCarMake = firstCarMakeElement.GetString();

            // Call the verifyIdentity function with the validated arguments
            return await verifyIdentity(lastName, firstCarMake);
        }
        catch (JsonException)
        {
            return "Invalid JSON format for verifyIdentity arguments.";
        }
    }
    else if (toolCall.FunctionName == checkWellnessBalanceTool.FunctionName)
    {
        // Validate arguments before using them; it's not always guaranteed to be valid JSON!
        try
        {
            string lastName = "";
            
            using JsonDocument argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
            if (!argumentsDocument.RootElement.TryGetProperty("lastName", out JsonElement lastNameElement))
            {
                return "Invalid arguments for checkWellnessBalance.";
            }

            lastName = lastNameElement.GetString();

            // Call the checkWellnessBalance function with the validated arguments
            return await checkWellnessBalance(lastName);
        }
        catch (JsonException)
        {
            return "Invalid JSON format for checkWellnessBalance arguments.";
        }
    }
    else if (toolCall.FunctionName == readReceiptTool.FunctionName)
    {
        // Validate arguments before using them; it's not always guaranteed to be valid JSON!
        try
        {
            using JsonDocument argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
            return await ReadReceipt();
        }
        catch (JsonException)
        {
            // Handle the JsonException (bad arguments) here
        }
    }
    else if (toolCall.FunctionName == sendEmailTool.FunctionName)
    {
        // Validate arguments before using them; it's not always guaranteed to be valid JSON!
        try
        {
            string emailSubject = "Test Email";
            string emailBody = "Hello World! Email sent.";
            
            using JsonDocument argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
            if (!argumentsDocument.RootElement.TryGetProperty("emailSubject", out JsonElement emailSubjectElement) ||
                !argumentsDocument.RootElement.TryGetProperty("emailBody", out JsonElement emailBodyElement))
            {
                // Handle missing required  argument with fixed location
                SendEmail(emailSubject, emailBody);
                return "Email sent successfully (Default)!";
            }
            else
            {
                emailSubject = emailSubjectElement.GetString();   
                emailBody = emailBodyElement.GetString();                             
                SendEmail(emailSubject, emailBody);
                return "Email sent successfully!";
            }
        }
        catch (JsonException)
        {
            // Handle the JsonException (bad arguments) here
        }
    }
    // Handle unexpected tool calls
    throw new NotImplementedException();
}


ChatCompletionOptions options = new()
{
    Tools = { getCurrentWeatherTool, verifyIdentityTool, checkWellnessBalanceTool, readReceiptTool, sendEmailTool },
};

var chatMessages = new List<ChatMessage>
    {
        new SystemChatMessage("You are a helpful assistant named Mike who is easy to interact with. Always verify the user's identity first when the session first starts. Ask the user what they need help with. When using webcam tools, ask the user for permission to proceed. Once a task is completed and the user confirms, ask if they need help with anything else. Be polite and brief. Your responses are streamed as text-to-speech downstream, so make sure to use full sentences and natural language to respond."),
    };

// Sentence end symbols for splitting the response into sentences.
List<string> sentenceSaperators = new() { ".", "!", "?", ";", "。", "！", "？", "；", "\n" };
try
{
    await ChatWithOpenAI();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

async Task AskOpenAI(string prompt)
{
    try
    {
        object consoleLock = new();
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);

        // The language of the voice that speaks.
        speechConfig.SpeechSynthesisVoiceName = "en-US-BrianNeural";
        var audioOutputConfig = AudioConfig.FromDefaultSpeakerOutput();
        using var speechSynthesizer = new SpeechSynthesizer(speechConfig, audioOutputConfig);
        speechSynthesizer.Synthesizing += (sender, args) =>
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                //Console.Write(args.Result);
                Console.ResetColor();
            }
        };

        Dictionary<int, string> toolCallIdsByIndex = [];
        Dictionary<int, string> functionNamesByIndex = [];
        Dictionary<int, StringBuilder> functionArgumentBuildersByIndex = [];
        var responseText = new StringBuilder();


        // Append to chat messages
        chatMessages.Add(new UserChatMessage(prompt));

        // Stream chat completion updates
        ResultCollection<StreamingChatCompletionUpdate> completionUpdates = chatClient.CompleteChatStreaming(chatMessages, options);

        foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
        {
            foreach (ChatMessageContentPart contentPart in completionUpdate.ContentUpdate)
            {
                responseText.Append(contentPart.Text);
            }
            foreach (StreamingChatToolCallUpdate toolCallUpdate in completionUpdate.ToolCallUpdates)
            {
                if (!string.IsNullOrEmpty(toolCallUpdate.Id))
                {
                    toolCallIdsByIndex[toolCallUpdate.Index] = toolCallUpdate.Id;
                }
                if (!string.IsNullOrEmpty(toolCallUpdate.FunctionName))
                {
                    functionNamesByIndex[toolCallUpdate.Index] = toolCallUpdate.FunctionName;
                }
                if (!string.IsNullOrEmpty(toolCallUpdate.FunctionArgumentsUpdate))
                {
                    StringBuilder argumentsBuilder
                        = functionArgumentBuildersByIndex.TryGetValue(toolCallUpdate.Index, out StringBuilder existingBuilder)
                            ? existingBuilder
                            : new();
                    argumentsBuilder.Append(toolCallUpdate.FunctionArgumentsUpdate);
                    functionArgumentBuildersByIndex[toolCallUpdate.Index] = argumentsBuilder;
                }
            }
        }

        List<ChatToolCall> toolCalls = [];
        foreach (KeyValuePair<int, string> indexToIdPair in toolCallIdsByIndex)
        {
            toolCalls.Add(ChatToolCall.CreateFunctionToolCall(
                indexToIdPair.Value,
                functionNamesByIndex[indexToIdPair.Key],
                functionArgumentBuildersByIndex[indexToIdPair.Key].ToString()));
        }

        if (toolCalls != null && toolCalls.Count > 0)
        {
            chatMessages.Add(new AssistantChatMessage(toolCalls, responseText.ToString()));
        }
        else
        {
            chatMessages.Add(new AssistantChatMessage(responseText.ToString()));
        }


        foreach (ChatToolCall toolCall in toolCalls)
        {
            chatMessages.Add(new ToolChatMessage(toolCall.Id, await GetToolCallOutput(toolCall)));
        }

        if (toolCalls != null && toolCalls.Count > 0)
        {
            // Stream tool completion updates
            ResultCollection<StreamingChatCompletionUpdate> toolCompletionUpdates = chatClient.CompleteChatStreaming(chatMessages, options);

            foreach (StreamingChatCompletionUpdate toolCompletionUpdate in toolCompletionUpdates)
            {
                foreach (ChatMessageContentPart contentPart in toolCompletionUpdate.ContentUpdate)
                {
                    responseText.Append(contentPart.Text);
                }
            }
        }

        // Synthesize the response
        await speechSynthesizer.SpeakTextAsync(responseText.ToString());
    }
    catch(Exception ex)
    {
        Console.WriteLine(chatMessages);
    }
}

// Continuously listens for speech input to recognize and send as text to Azure OpenAI
async Task ChatWithOpenAI()
{
    // Should be the locale for the speaker's language.
    var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
    speechConfig.SpeechRecognitionLanguage = "en-US";

    using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
    using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
    var conversationEnded = false;

    while (!conversationEnded)
    {
        Console.WriteLine("Azure OpenAI is listening. Say 'Stop' or press Ctrl-Z to end the conversation.");

        // Get audio from the microphone and then send it to the TTS service.
        var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();

        switch (speechRecognitionResult.Reason)
        {
            case ResultReason.RecognizedSpeech:
                if (speechRecognitionResult.Text == "Stop.")
                {
                    Console.WriteLine("Conversation ended.");
                    conversationEnded = true;
                }
                else if (speechRecognitionResult.Text == "Forget what I said.")
                {
                    chatMessages.Clear();
                    chatMessages = new List<ChatMessage>
                    {
                        new SystemChatMessage("You are a helpful assistant that is succinct and easy to interact with. Ask the user if they need help with anything. Once a task is completed and the user confirms, ask if they need help with anything else."),
                    };
                    Console.WriteLine("Conversation reset.");
                }
                else
                {
                    Console.WriteLine($"Recognized speech: {speechRecognitionResult.Text}");
                    await AskOpenAI(speechRecognitionResult.Text);
                }

                break;
            case ResultReason.NoMatch:
                Console.WriteLine($"No speech could be recognized: ");
                break;
            case ResultReason.Canceled:
                var cancellationDetails = CancellationDetails.FromResult(speechRecognitionResult);
                Console.WriteLine($"Speech Recognition canceled: {cancellationDetails.Reason}");
                if (cancellationDetails.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"Error details={cancellationDetails.ErrorDetails}");
                }

                break;
        }
    }
}