# App Service Mobile completed quickstart for Windows apps
This repository contains Windows app projects based on the App Service Mobile Apps quickstart project, which you can download from the [Azure portal](https://portal.azure.com), which has been enhanced by the addition of offline sync, authentication, and push notification functionality. This demonstrates how to best integrate the various Mobile Apps features. To learn how to download the Windows quickstart app project from the portal, see [Create a Windows app](https://azure.microsoft.com/documentation/articles/app-service-mobile-windows-store-dotnet-get-started/). This readme topic contains the following information to help you better understand the sample project.

+ [Overview](#overview)
+ [Configure the Mobile App backend](#configure-the-mobile-app-backend)
+ [Configure the Windows app](#configure-the-windows-app)
	+ [Configure authentication](#configure-authentication)
	+ [Configure push notifications](#configure-push-notifications)
+ [Running the app](#running-the-app)
+ [Implementation notes](#implementation-notes)
	+ [Template push notification registration](#template-push-notification-registration)
	+ [Client-added push notification tags](#client-added-push-notification-tags)
	+ [Authenticate first](#authenticate-first)

##Overview
The projects in this repository are equivalent to downloading the quickstart Windows app project from the portal and then completing the following Mobile Apps tutorials:

+ [Enable offline sync for your Windows app](https://azure.microsoft.com/documentation/articles/app-service-mobile-windows-store-dotnet-get-started-offline-data/)
+ [Add authentication to your Windows app](https://azure.microsoft.com/en-us/documentation/articles/app-service-mobile-windows-store-dotnet-get-started-users/)
+ [Add push notifications to your Windows app](https://azure.microsoft.com/en-us/documentation/articles/app-service-mobile-windows-store-dotnet-get-started-push/) 

## Configure the Mobile App backend

Before you can use this sample, you must have created and published a Mobile App backend project that supports  both authentication and push notifications (the backend supports offline sync by default). You can do this either by completing the previously indicated tutorials, or you can use one of the following Mobile Apps backend projects:

+ [.NET backend quickstart project for Mobile Apps](https://github.com/azure-samples/app-service-mobile-dotnet-backend-quickstart)
+ [Node.js backend quickstart project for Mobile Apps](https://github.com/azure-samples/app-service-mobile-nodejs-backend-quickstart)

The readme file in these projects will direct you to create a new Mobile App backend in App Service, then download, modify, and publish project to App Service.

After you have your new Mobile App backend running, you can configure this project to connect to that new backend.

## Configure the Windows app

The app project has offline sync support enabled, along with authentication and push notifications. However, you also need to configure authentication and push notifications before the app will run properly

### Configure authentication

Because both the client and backend are configured to use authentication, you must define an authentication provider for your app and register it with your Mobile App backend.

1. Follow the instructions in the topic to configure the Mobile App backend to use one of the following authentication providers:

	+ [AAD](https://azure.microsoft.com/documentation/articles/app-service-mobile-how-to-configure-active-directory-authentication/)
	+ [Facebook](https://azure.microsoft.com/documentation/articles/app-service-mobile-how-to-configure-facebook-authentication/)
	+ [Google](https://azure.microsoft.com/documentation/articles/app-service-mobile-how-to-configure-google-authentication/)
	+ [Microsoft account](https://azure.microsoft.com/documentation/articles/app-service-mobile-how-to-configure-microsoft-authentication/)
	+ [Twitter](https://azure.microsoft.com/documentation/articles/app-service-mobile-how-to-configure-twitter-authentication/)

2. By default, the app is configured to use server-directed Microsoft Account authentication. To use a different authentication provider, change the provider by locating this line of code in the shared MainPage.xaml.cs file and changing it to one of these other values: `AAD`, `Google`, `MicrosoftAccount`, or `Twitter`.

		var provider = "Facebook";

### Configure push notifications

You need to configure push notifications by registering your Windows app with the Windows Store then storing the app's package SID and client secret in the Mobile App backend. These credentials are used by Azure to connect to Windows Notification Service (WNS) to send push notifications. Complete the following sections of the push notifications tutorial to configure push notifications:

1. [Create a Notification Hub](https://github.com/Azure/azure-content-pr/blob/master/includes/app-service-mobile-create-notification-hub.md)
2. [Register your app for push notifications](https://github.com/Azure/azure-content-pr/blob/master/includes/app-service-mobile-register-wns.md)
3. [Configure the backend to send push notifications](https://github.com/Azure/azure-content-pr/blob/master/includes/app-service-mobile-configure-wns.md)

## Running the app

With both the Mobile App backend and the app configured, you can run the app project.

1. Right-click the Windows Store project, click **Set as StartUp Project**, then press the F5 key to run the Windows Store app.

2. In the app, click the **Sign-in** button and authenticate with the provider. 
	
	After authentication succeeds, the device is registered for push notifications and any existing data is downloaded from Azure.

2. Stop the Windows Store app and repeat the previous steps for the Windows Phone Store app.

	At this point, both devices are registered to receive push notifications.

3. Run the Windows Store app again, and type text in **Insert a TodoItem**, and then click **Save**.

   	Note that after the insert completes, both the Windows Store and the Windows Phone apps receive a push notification from WNS. The notification is displayed on Windows Phone even when the app isn't running.


## Implementation notes 
This section highlights changes made to the original tutorial samples and other design decisions were made when implementing all of the features or Mobile Apps in the same client app. 

###Template push notification registration
The original push notification tutorial used a native WNS registration. This sample has been changed to use a template registration, which makes it easier to send push notifications to users on multiple clients from a single **send** method call. The following code defines the toast template registration:

    // Define a toast templates for WNS.
    var toastTemplate =
        @"<toast><visual><binding template=""ToastText02""><text id=""1"">"
                        + @"New item:</text><text id=""2"">"
                        + @"$(message)</text></binding></visual></toast>";

    JObject templateBody = new JObject();
    templateBody["body"] = toastTemplate;

    // Add the required WNS toast header.
    JObject wnsToastHeaders = new JObject();
    wnsToastHeaders["X-WNS-Type"] = "wns/toast";
    templateBody["headers"] = wnsToastHeaders;

    JObject templates = new JObject();
    templates["testTemplate"] = templateBody;


For more information, see [How to: Register push templates to send cross-platform notifications](https://azure.microsoft.com/documentation/articles/app-service-mobile-dotnet-how-to-use-client-library/#how-to-register-push-templates-to-send-cross-platform-notifications).

###Client-added push notification tags


###Authenticate first
This sample is a little different from the tutorials in that push notifications are send to all devices with push registrations that belong to a specific user. When an authenticated user registers for push notifications, a tag with the user ID is automatically added. Because of this, it's important to have the user sign-in before registering for push notifications. You should also have the user sign-in before executing any data or sync requests, which will result in an exception when the endpoint requires authentication. You also probably don't want an unauthenticated user to see offline data stored on the device. The following button Click event handler shows how to require explicit user sign-in before push registration and doing the initial data sync:

    private async void ButtonLogin_Click(object sender, RoutedEventArgs e)
    {
        // Login the user and then load data from the mobile app.
        if (await AuthenticateAsync())
        {
            // Register for push notifications.
            InitNotificationsAsync();

            // Hide the login button and load items from the mobile app.
            ButtonLogin.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            await InitLocalStoreAsync(); //offline sync support.
            await RefreshTodoItems();
        }
    }


