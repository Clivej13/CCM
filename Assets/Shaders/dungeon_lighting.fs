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
uniform float occluderCount;
uniform vec3 occluderMin[32];
uniform vec3 occluderMax[32];
uniform vec4 characterCaster0;
uniform vec4 characterCaster1;
uniform vec2 characterHeights;
uniform float receiveCharacterShadows;

out vec4 finalColor;

bool boxBlocks(vec3 from, vec3 to, vec3 boxMin, vec3 boxMax)
{
    vec3 direction = to - from;
    float enter = 0.0;
    float exit = 1.0;
    for (int axis = 0; axis < 3; axis++)
    {
        float delta = direction[axis];
        if (abs(delta) < 0.00001)
        {
            if (from[axis] < boxMin[axis] || from[axis] > boxMax[axis])
                return false;
            continue;
        }
        float a = (boxMin[axis] - from[axis]) / delta;
        float b = (boxMax[axis] - from[axis]) / delta;
        enter = max(enter, min(a, b));
        exit = min(exit, max(a, b));
        if (enter > exit) return false;
    }
    return enter > 0.001 && enter < 0.995;
}

bool lightBlocked(vec3 from, vec3 to)
{
    if (boxBlocks(from, to, doorMin, doorMax)) return true;
    for (int i = 0; i < 32; i++)
    {
        if (float(i) >= occluderCount) break;
        if (boxBlocks(from, to, occluderMin[i], occluderMax[i])) return true;
    }
    return false;
}

float characterShadow(vec3 from, vec3 to, vec4 caster, float height)
{
    if (caster.w <= 0.0) return 0.0;
    vec3 ray = to - from;
    float low = 0.0;
    float high = 1.0;
    if (abs(ray.y) < 0.00001)
    {
        if (from.y < caster.y || from.y > caster.y + height) return 0.0;
    }
    else
    {
        float a = (caster.y - from.y) / ray.y;
        float b = (caster.y + height - from.y) / ray.y;
        low = max(low, min(a, b));
        high = min(high, max(a, b));
        if (low >= high) return 0.0;
    }
    vec2 start = mix(from.xz, to.xz, low);
    vec2 end = mix(from.xz, to.xz, high);
    vec2 span = end - start;
    float along = clamp(dot(caster.xz - start, span) /
        max(dot(span, span), 0.000001), 0.0, 1.0);
    float distance = length(start + along * span - caster.xz);
    return 1.0 - smoothstep(caster.w * 0.7, caster.w * 1.5, distance);
}

vec3 fireplaceLight(vec3 position, vec3 color, float visibility, vec3 normal)
{
    if (visibility <= 0.0 || lightBlocked(position, fragPosition))
        return vec3(0.0);
    vec3 direction = position - fragPosition;
    float distanceSquared = dot(direction, direction);
    float facing = max(dot(normal, normalize(direction)), 0.0);
    float diffuse = 0.28 + 0.72 * facing;
    float range = max(0.0, 1.0 - distanceSquared / 400.0);
    float attenuation = range / (1.0 + distanceSquared * 0.018);
    float shadow = 0.0;
    if (receiveCharacterShadows > 0.5)
    {
        shadow = max(characterShadow(position, fragPosition, characterCaster0,
            characterHeights.x),
            characterShadow(position, fragPosition, characterCaster1,
            characterHeights.y));
    }
    return color * visibility * diffuse * attenuation * (1.0 - 0.78 * shadow);
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
