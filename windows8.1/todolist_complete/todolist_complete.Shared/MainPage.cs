using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

using Microsoft.WindowsAzure.MobileServices.SQLiteStore; 
using Microsoft.WindowsAzure.MobileServices.Sync;         

using System.Linq;
using Windows.Security.Credentials;
using Newtonsoft.Json.Linq;

using Windows.Networking.PushNotifications;
using System.Net.Http;

namespace todolist_complete
{
    sealed partial class MainPage: Page
    {
        private MobileServiceCollection<TodoItem, TodoItem> items;
        private IMobileServiceSyncTable<TodoItem> todoTable = App.MobileService.GetSyncTable<TodoItem>(); //Offline table. 
        //private IMobileServiceTable<TodoItem> todoTable = App.MobileService.GetTable<TodoItem>();

        public MainPage()
        {
            this.InitializeComponent();
        }
        // Push authentication code added in http://aka.ms/m79ei6
        #region push notifications
        // Registers for template push notifications.
        private async void InitNotificationsAsync()
        {
            var channel = await PushNotificationChannelManager
                .CreatePushNotificationChannelForApplicationAsync();

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

            try
            {
                // Register for template push notifications.
                await App.MobileService.GetPush()
                .RegisterAsync(channel.Uri, templates);

                // Define two new tags as a JSON array.
                var body = new JArray();
                body.Add("broadcast");
                body.Add("test");

                // Call the custom API '/api/updatetags/<installationid>' 
                // with the JArray of tags.
                var response = await App.MobileService
                    .InvokeApiAsync("updatetags/" 
                    + App.MobileService.InstallationId, body);
            }
            catch (Exception)
            {
                await new MessageDialog("Push registration failed.").ShowAsync();
            }
        }

        //// Registers for native WNS push.
        //private async Task InitNotificationsAsync()
        //{
        //    // Get a channel URI from WNS.
        //    var channel = await PushNotificationChannelManager
        //        .CreatePushNotificationChannelForApplicationAsync();

        //    // Register the channel URI with Notification Hubs.
        //    await App.MobileService.GetPush().RegisterAsync(channel.Uri);
        //}
        #endregion 

        // Authenticate code added in http://aka.ms/qqyoe8
        #region authentication
        // Define a member variable for storing the signed-in user. 
        private MobileServiceUser user;

        private async System.Threading.Tasks.Task<bool> AuthenticateAsync()
        {
            string message;
            bool success = false;

            // This sample uses the Facebook provider.
            var provider = "Facebook";

            // Use the PasswordVault to securely store and access credentials.
            PasswordVault vault = new PasswordVault();
            PasswordCredential credential = null;

            try
            {
                var credentials = vault.FindAllByResource(provider);
                // Try to get an existing credential from the vault.
                credential = vault.FindAllByResource(provider).FirstOrDefault();
            }
            catch (Exception)
            {
                // When there is no matching resource an error occurs, which we ignore.
            }

            // If we have a valid unexpired token, use it--otherwise sign-in again.
            if (credential != null && !App.MobileService.IsTokenExpired(credential))
            {
                // Create a user from the stored credentials.
                user = new MobileServiceUser(credential.UserName);
                credential.RetrievePassword();
                user.MobileServiceAuthenticationToken = credential.Password;

                // Set the user from the stored credentials.
                App.MobileService.CurrentUser = user;

                // Consider adding a check to determin if the token is 
                // expired, as shown in http://aka.ms/jww5vp.

                success = true;
                message = string.Format("Cached credentials for user - {0}", user.UserId);

            }
            else
            {
                try
                {
                    // If we have an expired token, remove it.
                    vault.Remove(credential);

                    // Login with the identity provider.
                    user = await App.MobileService
                        .LoginAsync(provider);

                    // Create and store the user credentials.
                    credential = new PasswordCredential(provider,
                        user.UserId, user.MobileServiceAuthenticationToken);
                    vault.Add(credential);

                    success = true;
                    message = string.Format("You are now logged in - {0}", user.UserId);
                }

                catch (InvalidOperationException)
                {
                    message = "You must log in. Login Required";
                }
            }

            await new MessageDialog(message).ShowAsync();

            return success;
        }

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
        #endregion

        // Supports offline sync, described in http://aka.ms/mwqjul
        #region Offline sync
        private async Task InitLocalStoreAsync()
        {
            if (!App.MobileService.SyncContext.IsInitialized)
            {
                var store = new MobileServiceSQLiteStore("localstore.db");
                store.DefineTable<TodoItem>();
                await App.MobileService.SyncContext.InitializeAsync(store);
            }

            await SyncAsync();
        }

        private async Task SyncAsync()
        {
            String errorString = null;

            try
            {
                await App.MobileService.SyncContext.PushAsync();

                // first param is query ID, used for incremental sync
                await todoTable.PullAsync("todoItems", todoTable.CreateQuery());
            }

            catch (MobileServicePushFailedException ex)
            {
                errorString = "Push failed because of sync errors. You may be offine.\nMessage: " +
                  ex.Message + "\nPushResult.Status: " + ex.PushResult.Status.ToString();
            }
            catch (Exception ex)
            {
                errorString = "Pull failed: " + ex.Message +
                  "\n\nIf you are still in an offline scenario, " +
                  "you can try your Pull again when connected with your Mobile Serice.";
            }

            if (errorString != null)
            {
                MessageDialog d = new MessageDialog(errorString);
                await d.ShowAsync();
            }
        }

        #endregion 

        private async Task InsertTodoItem(TodoItem todoItem)
        {
            // This code inserts a new TodoItem into the database. When the operation completes
            // and Mobile App backend has assigned an Id, the item is added to the CollectionView.
            await todoTable.InsertAsync(todoItem);
            items.Add(todoItem);

            await SyncAsync(); // offline sync
        }

        private async Task RefreshTodoItems()
        {
            MobileServiceInvalidOperationException exception = null;
            try
            {
                // This code refreshes the entries in the list view by querying the TodoItems table.
                // The query excludes completed TodoItems.
                items = await todoTable
                    .Where(todoItem => todoItem.Complete == false)
                    .ToCollectionAsync();
            }
            catch (MobileServiceInvalidOperationException e)
            {
                exception = e;
            }

            if (exception != null)
            {
                await new MessageDialog(exception.Message, "Error loading items").ShowAsync();
            }
            else
            {
                ListItems.ItemsSource = items;
                this.ButtonSave.IsEnabled = true;
            }
        }

        private async Task UpdateCheckedTodoItem(TodoItem item)
        {
            // This code takes a freshly completed TodoItem and updates the database. When the service 
            // responds, the item is removed from the list.
            await todoTable.UpdateAsync(item);
            items.Remove(item);
            ListItems.Focus(Windows.UI.Xaml.FocusState.Unfocused);

            await SyncAsync(); // offline sync
        }

        private async void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            ButtonRefresh.IsEnabled = false;

            await SyncAsync(); // offline sync
            await RefreshTodoItems();

            ButtonRefresh.IsEnabled = true;
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            var todoItem = new TodoItem { Text = TextInput.Text };
            await InsertTodoItem(todoItem);
        }

        private async void CheckBoxComplete_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            TodoItem item = cb.DataContext as TodoItem;
            await UpdateCheckedTodoItem(item);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //await InitLocalStoreAsync(); // offline sync
            //await RefreshTodoItems();
        }
    }
}
