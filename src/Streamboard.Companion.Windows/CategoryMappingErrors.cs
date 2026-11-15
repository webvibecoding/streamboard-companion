using System.Net;
using System.Net.Http;
using Streamboard.Companion.Core.Api;

namespace Streamboard.Companion.Windows;

internal static class CategoryMappingErrors
{
    public static bool ConnectionRequired(Exception exception) =>
        exception is CompanionApiException apiException && CategoryMappings.IsDeviceUnauthorized(apiException);

    public static string Message(Exception exception, string fallback) => exception switch
    {
        CompanionApiException apiException when CategoryMappings.IsDeviceUnauthorized(apiException) =>
            "Подключите Streamboard через меню в трее и повторите попытку.",
        CompanionApiException { Reason: "twitch_not_connected" or "twitch_auth_revoked" } =>
            "Подключите Twitch в веб-панели Streamboard.",
        CompanionApiException { Reason: "vkplaylive_not_connected" or "vkplaylive_auth_revoked" } =>
            "Подключите VK Play Live в веб-панели Streamboard.",
        CompanionApiException { StatusCode: HttpStatusCode.TooManyRequests } =>
            "Слишком много запросов. Повторите попытку позже.",
        CompanionApiException { StatusCode: >= HttpStatusCode.InternalServerError } =>
            "Сервис временно недоступен. Повторите попытку позже.",
        HttpRequestException or OperationCanceledException =>
            "Нет ответа от Streamboard. Проверьте соединение и повторите попытку.",
        CompanionApiException apiException => $"{fallback} Код: {apiException.Reason}",
        _ => fallback,
    };
}
