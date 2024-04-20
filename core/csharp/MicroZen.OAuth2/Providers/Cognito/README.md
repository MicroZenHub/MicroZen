![MicroZen Logo](../../../../../assets/microzen_color_logo.png)

# ![AWS Cognito Logo](../../../../../assets/providers/cognito.png) AWS Cognito Provider

The AWS Cognito provider allows you to authenticate against AWS Cognito User Pools.

## Configuration

1. To configure this provider, you will need to create a new User Pool and client in AWS Cognito. Once you have created the User Pool, you will need to configure the following settings:

### JSON format

 `appsettings.json` or `appsettings.{ASPNETCORE_ENVIRONMENT}.json` file

    ```json
    {
        "AWS": {
            "Cognito": {
                "Region": "us-east-1",
                "UserPoolId": "us-east-1_XXXXXXXXX"
            }
        }
    }
    ```

### Environment Variable Example

 ```bash
AWS__Cogntio__Region=us-east-1
AWS__Cogntio__UserPoolId=us-east-1_XXXXXXXXX
```

## Usage

1. Add config values as specified above
2. [Follow instructions to get started with the MicroZen.OAuth2 package](../../README.md)
