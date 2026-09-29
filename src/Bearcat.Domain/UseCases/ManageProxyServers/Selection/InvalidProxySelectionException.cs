namespace Bearcat.Domain.UseCases.ManageProxyServers.Selection;

public sealed class InvalidProxySelectionException(string message) : Exception(message);
