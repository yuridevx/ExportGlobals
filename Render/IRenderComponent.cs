namespace Visual.Render;

/// <summary>
/// Interface for visualization components that render to the screen.
/// </summary>
public interface IRenderComponent
{
    /// <summary>
    /// Whether this component is enabled and should render.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Called every frame to render the component.
    /// Uses Global.Graphics and Global.Controller for access.
    /// </summary>
    void Render();
}
