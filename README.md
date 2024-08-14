# MIKE: OpenAI Speech-to-Action Agent

This .NET console application implements an AI agent and integrates with OpenAI (gpt4-omni and gpt4-omni mini) to provide various functionalities such as weather information, identity verification, wellness balance checks, image analysis, and email sending. The AI agent responds to voice commands to execute various actions.

## Features

- **Weather Information**: Retrieve current weather details for a specified location.
- **Identity Verification**: Verify user identity based on last name and first car make.
- **Wellness Balance Check**: Check the wellness balance of a user.
- **Image Analysis**: Capture and analyze images using OCR to extract details from receipts.
- **Email Sending**: Send emails with receipt summaries.

## Prerequisites

- .NET SDK
- Azure Subscription
- OpenAI API Keys (Omni-mini used for conversational, Omni used for image analysis)
- Azure Communication Service Connection String
- Azure Cognitive Services Speech API Key

## Configuration

Create an `appsettings.json` file in the root directory with the following structure:

```json
{
    "OpenAI": {
      "Key": "YOUR_OPENAI_KEY",
      "Endpoint": "https://YOUR_OPENAI_ENDPOINT",
      "DeploymentName": "YOUR_CONVERSATION_DEPLOYMENT_NAME",
      "imageKey": "YOUR_IMAGE_KEY",
      "imageEndpoint": "https://YOUR_IMAGE_ENDPOINT",
      "ImageDeploymentName": "YOUR_IMAGE_DEPLOYMENT_NAME"
    },
    "Speech": {
      "Key": "YOUR_SPEECH_KEY",
      "Region": "YOUR_SPEECH_REGION"
    },
    "CommunicationService": {
      "ConnectionString": "YOUR_COMMUNICATION_SERVICE_CONNECTION_STRING",
      "Domain": "YOUR_COMMUNICATION_SERVICE_DOMAIN"
    }
}
```

## Example JSON Data

Use the following anonymized JSON data for verification and wellness balance checks, and make sure to update the code to point to this JSON file location for data extraction:

```json
{
    "users": [
        {
            "firstName": "John",
            "lastName": "Doe",
            "firstCarMake": "Toyota",
            "wellnessPool": 500.00
        },
        {
            "firstName": "Jane",
            "lastName": "Smith",
            "firstCarMake": "Honda",
            "wellnessPool": 200.00
        }
    ]
}
```

## How to Run

1. **Clone the repository**:
   ```sh
   git clone https://github.com/your-repo/openai-agent-console.git
   cd openai-agent-console
   ```

2. **Restore dependencies**:
   ```sh
   dotnet restore
   ```

3. **Build the project**:
   ```sh
   dotnet build
   ```

4. **Run the application**:
   ```sh
   dotnet run
   ```

## Usage

- The application listens for voice commands.
- Say "Stop" to end the conversation.
- Say "Forget what I said" to reset the conversation.

## Tools

- **GetCurrentWeather**: Fetches current weather information.
- **VerifyIdentity**: Verifies user identity.
- **CheckWellnessBalance**: Checks user's wellness balance.
- **ReadReceipt**: Captures and analyzes receipt images.
- **SendEmail**: Sends an email with the provided subject and body.

## License

This project is licensed under the MIT License. See the LICENSE file for details.