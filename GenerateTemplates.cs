public static class GenerateTemplates
{
    public static string pythonCode = @"
import Utils
from Options import generate_yaml_templates

def generate_yamls():
    target = Utils.user_path('Players', 'Templates')
    generate_yaml_templates(target, False)
    print(f'Templates generated in: {target}')

if __name__ == '__main__':
    generate_yamls()
";
}
