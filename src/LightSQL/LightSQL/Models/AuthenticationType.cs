namespace LightSQL.Models
{
    /// <summary>
    /// The way a connection authenticates against SQL Server.
    /// Values start at 1 so an unset (default) value is never a valid choice.
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// Windows / Integrated authentication. No password is stored.
        /// </summary>
        Windows = 1,

        /// <summary>
        /// SQL Server authentication using a login name and password.
        /// </summary>
        SqlServer = 2
    }
}
