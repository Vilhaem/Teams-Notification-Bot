using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace NotificationBot.Swagger
{
    /// <summary>
    /// Test Filter for customizing SwaggerUI
    /// </summary>
    public class UtilityAudioParameterFilter : IParameterFilter
    {
        public void Apply(OpenApiParameter parameter, ParameterFilterContext context)
        {
            IEnumerable<UtilityAudioClipsParameterAttribute>? attributes = null;

            if (context.PropertyInfo is not null)
            {
                attributes = context.PropertyInfo.GetCustomAttributes<UtilityAudioClipsParameterAttribute>();
            }
            else if (context.ParameterInfo is not null)
            {
                attributes = context.ParameterInfo.GetCustomAttributes<UtilityAudioClipsParameterAttribute>();
            }
            if (attributes is not null && attributes.Any()) 
            { 

            }
        }
        private void AddExample(OpenApiParameter parameter, IEnumerable<UtilityAudioClipsParameterAttribute> attributes)
        {
            foreach (var item in attributes) 
            {
                var example = new OpenApiExample
                {
                    Value = new OpenApiString(item.Value)
                };
                parameter.Examples.Add(item.Name, example);
            }
        }
    }
}
