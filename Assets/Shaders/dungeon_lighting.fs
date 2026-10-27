#version 330
in vec3 fragPosition;
in vec3 fragNormal;
in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 lightPosition0;
uniform vec3 lightPosition1;
uniform vec3 lightPosition2;
uniform vec3 lightPosition3;
uniform vec3 lightPosition4;
uniform vec3 lightPosition5;
uniform vec3 lightPosition6;
uniform vec3 lightPosition7;
uniform vec3 lightPosition8;
uniform vec3 lightColor0;
uniform vec3 lightColor1;
uniform vec3 lightColor2;
uniform vec3 lightColor3;
uniform vec3 lightColor4;
uniform vec3 lightColor5;
uniform vec3 lightColor6;
uniform vec3 lightColor7;
uniform vec3 lightColor8;
uniform float lightVisibility0;
uniform float lightVisibility1;
uniform float lightVisibility2;
uniform float lightVisibility3;
uniform float lightVisibility4;
uniform float lightVisibility5;
uniform float lightVisibility6;
uniform float lightVisibility7;
uniform float lightVisibility8;
uniform vec3 doorMin;
uniform vec3 doorMax;

out vec4 finalColor;

bool doorBlocks(vec3 from, vec3 to)
{
    vec3 direction = to - from;
    float enter = 0.0;
    float exit = 1.0;
    for (int axis = 0; axis < 3; axis++)
    {
        float delta = direction[axis];
        if (abs(delta) < 0.00001)
        {
            if (from[axis] < doorMin[axis] || from[axis] > doorMax[axis])
                return false;
            continue;
        }
        float a = (doorMin[axis] - from[axis]) / delta;
        float b = (doorMax[axis] - from[axis]) / delta;
        enter = max(enter, min(a, b));
        exit = min(exit, max(a, b));
        if (enter > exit) return false;
    }
    return enter > 0.001 && enter < 0.995;
}

vec3 fireplaceLight(vec3 position, vec3 color, float visibility, vec3 normal)
{
    if (visibility <= 0.0 || doorBlocks(position, fragPosition))
        return vec3(0.0);
    vec3 direction = position - fragPosition;
    float distanceSquared = dot(direction, direction);
    float facing = max(dot(normal, normalize(direction)), 0.0);
    float diffuse = 0.28 + 0.72 * facing;
    float range = max(0.0, 1.0 - distanceSquared / 400.0);
    float attenuation = range / (1.0 + distanceSquared * 0.018);
    return color * visibility * diffuse * attenuation;
}

void main()
{
    vec4 albedo = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
    vec3 normal = normalize(fragNormal);
    vec3 light = vec3(0.0);
    light += fireplaceLight(lightPosition0, lightColor0, lightVisibility0, normal);
    light += fireplaceLight(lightPosition1, lightColor1, lightVisibility1, normal);
    light += fireplaceLight(lightPosition2, lightColor2, lightVisibility2, normal);
    light += fireplaceLight(lightPosition3, lightColor3, lightVisibility3, normal);
    light += fireplaceLight(lightPosition4, lightColor4, lightVisibility4, normal);
    light += fireplaceLight(lightPosition5, lightColor5, lightVisibility5, normal);
    light += fireplaceLight(lightPosition6, lightColor6, lightVisibility6, normal);
    light += fireplaceLight(lightPosition7, lightColor7, lightVisibility7, normal);
    light += fireplaceLight(lightPosition8, lightColor8, lightVisibility8, normal);
    finalColor = vec4(albedo.rgb * min(light, vec3(1.55)), albedo.a);
}
