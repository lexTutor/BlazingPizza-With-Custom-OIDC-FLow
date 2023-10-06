using BlazingPizza.Shared;

namespace BlazingPizza.Client.Data
{
    public class OrdersClient
    {
        private readonly HttpClient httpClient;
        private readonly TokenProvider tokenProvider;

        public OrdersClient(HttpClient httpClient, TokenProvider tokenProvider)
        {
            this.httpClient = httpClient;
            this.tokenProvider = tokenProvider;
        }

        public async Task<IEnumerable<OrderWithStatus>> GetOrders()
        {
            SetAuthToken();

            return await httpClient.GetFromJsonAsync<IEnumerable<OrderWithStatus>>("orders");
        }

        public async Task<OrderWithStatus> GetOrder(int orderId)
        {
            SetAuthToken();

            return await httpClient.GetFromJsonAsync<OrderWithStatus>($"orders/{orderId}");
        }

        public async Task<int> PlaceOrder(Order order)
        {
            SetAuthToken();

            var response = await httpClient.PostAsJsonAsync("orders", order);

            response.EnsureSuccessStatusCode();
            var orderId = await response.Content.ReadFromJsonAsync<int>();
            return orderId;
        }

        public async Task SubscribeToNotifications(NotificationSubscription subscription)
        {
            SetAuthToken();

            var response = await httpClient.PutAsJsonAsync("notifications/subscribe", subscription);

            var rrr = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();
        }

        private void SetAuthToken()
        {
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenProvider.AccessToken);
        }
    }

}
