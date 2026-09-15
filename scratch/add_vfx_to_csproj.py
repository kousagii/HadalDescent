with open('Assembly-CSharp.csproj', 'r', encoding='utf-8') as f:
    text = f.read()

target = '<Compile Include="Assets\\Scripts\\Environment\\EnvironmentFactTarget.cs" />'
repl = target + '\n    <Compile Include="Assets\\Scripts\\Environment\\SunlightAtmosphereVFX.cs" />'

if target in text and 'SunlightAtmosphereVFX.cs' not in text:
    text = text.replace(target, repl, 1)
    with open('Assembly-CSharp.csproj', 'w', encoding='utf-8') as f:
        f.write(text)
    print("Added SunlightAtmosphereVFX.cs to Assembly-CSharp.csproj")
else:
    print("Already present or target not found")
