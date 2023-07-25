namespace NotificationBot.Swagger
{
    [AttributeUsage(AttributeTargets.Parameter|AttributeTargets.Property, AllowMultiple = true)]
    public class UtilityAudioClipsParameterAttribute:Attribute
    {
        /// <summary>
        /// Test Parameter Attribute for Swagger UI
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public UtilityAudioClipsParameterAttribute(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; set; }
        public string Value { get; set; }
    }
    
}
