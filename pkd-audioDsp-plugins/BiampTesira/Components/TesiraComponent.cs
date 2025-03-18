namespace BiampTesira.Components;

/// <summary>
/// Base implementation of any Biamp Tesira control component, such as level blocks, matrix mixers, or router controls.
/// </summary>
/// <param name="id"></param>
/// <param name="instanceTag">The tag in the Tesira design for controlling the component.</param>
internal abstract class TesiraComponent(string id, string instanceTag)
{
   /// <summary>
   /// A unique id for this component. This will be used in the subscribe/unsubscribe commands.
   /// </summary>
    public string Id { get; private set; } = id;
   
   /// <summary>
   /// The tag in the Tesira design for controlling the component.
   /// </summary>
    public string InstanceTag { get; private set; } = instanceTag;
   
    /// <returns>A collection of all formatted commands for subscribing to change events related to this component.</returns>
    public abstract List<string> GetSubscribeCommands();
    
    /// <returns>A collection of all formatted commands for unsubscribing to change events related to this component.</returns>
    public abstract List<string> GetUnsubscribeCommands();
    
    /// <summary>
    /// Parses the raw command and updates internal states accordingly.
    /// </summary>
    /// <param name="response">The raw response from the device.</param>
    public abstract void HandleResponse(string response);
}