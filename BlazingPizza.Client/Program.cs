using BlazingPizza.Client.Data;
using BlazingPizza.Shared;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<HttpClient>();
builder.Services.AddHttpClient<OrdersClient>(client => client.BaseAddress = new Uri("https://localhost:7241/orders"));
builder.Services.AddScoped<TokenProvider>();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<OrderState>();

builder.Services.AddOpenIddict(o =>
{
    o.AddClient(o =>
    {
        o.AllowAuthorizationCodeFlow();
        o.SetClientUri("https://localhost:7066");
    });
});


builder.Services.AddAuthentication(sharedOptions =>
{
    sharedOptions.DefaultAuthenticateScheme =
         CookieAuthenticationDefaults.AuthenticationScheme;
    sharedOptions.DefaultSignInScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;
    sharedOptions.DefaultChallengeScheme =
       OpenIdConnectDefaults.AuthenticationScheme;
})
 .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
 {
     options.LoginPath = "/login";
     options.Events = new CookieAuthenticationEvents
     {
         // After the auth cookie has been validated, this event is called.
         // In it we see if the access token is close to expiring.  If it is
         // then we use the refresh token to get a new access token and save them.
         // If the refresh token does not work for some reason then we redirect to 
         // the login screen.
         OnValidatePrincipal = async cookieCtx =>
         {
             try
             {
                 var expiresAt = cookieCtx.Properties.Items[".Token.expires_at"];
                 var accessTokenExpiration = DateTimeOffset.Parse(expiresAt);

                 var timeRemaining = accessTokenExpiration.Subtract(DateTimeOffset.UtcNow);

                 // TODO: Get this from configuration with a fall back value.
                 var refreshThresholdMinutes = 1;
                 var refreshThreshold = TimeSpan.FromMinutes(refreshThresholdMinutes);

                 if (timeRemaining < refreshThreshold)
                 {
                     var refreshToken = cookieCtx.Properties.GetTokenValue("refresh_token");

                     // TODO: Get this HttpClient from a factory
                     var response = await new HttpClient().RequestRefreshTokenAsync(new RefreshTokenRequest
                     {
                         Address = "https://localhost:7066/connect/token",
                         ClientId = "000333",
                         RefreshToken = refreshToken
                     });

                     if (!response.IsError)
                     {
                         var expiresInSeconds = response.ExpiresIn;
                         var updatedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
                         cookieCtx.Properties.UpdateTokenValue("expires_at", updatedExpiresAt.ToString());
                         cookieCtx.Properties.UpdateTokenValue("access_token", response.AccessToken);
                         cookieCtx.Properties.UpdateTokenValue("refresh_token", response.RefreshToken);

                         // Indicate to the cookie middleware that the cookie should be remade (since we have updated it)
                         cookieCtx.ShouldRenew = true;
                     }
                     else
                     {
                         cookieCtx.RejectPrincipal();
                         await cookieCtx.HttpContext.SignOutAsync();
                     }
                 }
             }
             catch (Exception ex)
             {
                 cookieCtx.RejectPrincipal();
                 await cookieCtx.HttpContext.SignOutAsync();
             }

             await Task.CompletedTask;
         }
     };
 })
 .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = "https://localhost:7066/";
        options.ClientId = "000333";
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.UsePkce = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.UseTokenLifetime = false;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("blazzing_pizza_user");
        options.Scope.Add("offline_access");

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                context.Response.Redirect("/");
                context.HandleResponse();
                return Task.CompletedTask;
            },
            OnAccessDenied = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/");
                return Task.CompletedTask;
            }
        };
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
