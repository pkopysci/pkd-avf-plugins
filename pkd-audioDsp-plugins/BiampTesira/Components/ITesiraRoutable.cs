using pkd_common_utils.GenericEventArgs;

namespace BiampTesira.Components;

internal interface ITesiraRoutable
{
    /// <summary>
    /// args package: arg1 = id of the input channel that was routed, arg2 = id of the output that changed.
    /// </summary>
    event EventHandler<GenericDualEventArgs<string, string>> RouteChanged; 
    
    /// <summary>
    /// The total number if input channels on the component.
    /// </summary>
    uint MaxInputs { get; }
    
    /// <summary>
    /// The total number of output channels on the component.
    /// </summary>
    uint MaxOutputs { get; }
    
    /// <param name="inputId">The id of the input channel that was previously added with <see cref="TryAddInput"/>.</param>
    /// <param name="outputId">The id of the output channel that was previously added with <see cref="TryAddOutput"/>.</param>
    /// <returns>a formatted command for routing the input to the output.</returns>
    string GetMakeRouteCommand(string inputId, string outputId);
    
    /// <returns>A collection of commands for clearing the route for each output in the component.</returns>
    List<string> GetClearAllRoutesCommand();
    
    /// <summary>
    /// Get a list of commands for querying the current state of all component outputs.
    /// </summary>
    /// <returns>A list of commands for querying all current routes on the component.</returns>
    List<string> GetQueryAllRoutesCommands();
    
    /// <param name="outputId">he id of the output channel that was previously added with <see cref="TryAddOutput"/>.</param>
    /// <returns>the input index routed to the output as of the last response from the device.</returns>
    int QueryRoute(string outputId);

    /// <summary>
    /// Attempt to add a <see cref="TesiraChannel"/> input to the internal collection of channels assigned to this routing component.
    /// </summary>
    /// <param name="channel">The unique channel to add.</param>
    /// <returns>True if successfully added the channel, false if a channel with a matching id or index already exists.</returns>
    bool TryAddInput(TesiraChannel channel);
    
    /// <summary>
    /// Attempt to add a <see cref="TesiraChannel"/> output to the internal collection of channels assigned to this routing component.
    /// </summary>
    /// <param name="channel">The unique channel to add.</param>
    /// <returns>True if successfully added the channel, false if a channel with a matching id or index already exists.</returns>
    bool TryAddOutput(TesiraChannel channel);
    
    /// <summary>
    /// Search for a <see cref="TesiraChannel"/> input with a matching id.
    /// </summary>
    /// <param name="id">the unique id of the input to search for.</param>
    /// <param name="channel">the reference object that will be assigned if the channel is found.</param>
    /// <returns>True if the channel was found, false otherwise.</returns>
    bool TryFindInput(string id, out TesiraChannel channel);
    
    /// <summary>
    /// Search for a <see cref="TesiraChannel"/> output with a matching id.
    /// </summary>
    /// <param name="id">the unique id of the output to search for.</param>
    /// <param name="channel">the reference object that will be assigned if the channel is found.</param>
    /// <returns>True if the channel was found, false otherwise.</returns>
    bool TryFindOutput(string id, out TesiraChannel channel);
}