using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Globalization;
using System.Threading.Tasks;

class Atividade
{
    public string? code { get; set; }
    [JsonProperty("text")]
    public string? texto { get; set; }
}

class Simples
{
    public string? optante { get; set; }
    public string? data_opcao { get; set; }
    public string? data_exclusao { get; set; }
    public string? ultima_atualizacao { get; set; }
}

class Empresa
{
    public string? abertura { get; set; }
    public string? situacao { get; set; }
    public string? tipo { get; set; }
    public string? nome { get; set; }
    public string? porte { get; set; }
    public string? natureza_juridica { get; set; }
    public List<Atividade>? atividade_principal { get; set; }
    public List<Atividade>? atividades_secundarias { get; set; }
    public string? logradouro { get; set; }
    public string? numero { get; set; }
    public string? municipio { get; set; }
    public string? bairro { get; set; }
    public string? uf { get; set; }
    public string? cep { get; set; }
    public string? data_situacao { get; set; }
    public string? cnpj { get; set; }
    public string? ultima_atualizacao { get; set; }
    public string? status { get; set; }
    public string? fantasia { get; set; }
    public string? complemento { get; set; }
    public string? email { get; set; }
    public string? telefone { get; set; }
    public string? efr { get; set; }
    public string? motivo_situacao { get; set; }
    public string? situacao_especial { get; set; }
    public string? data_situacao_especial { get; set; }
    public string? capital_social { get; set; }
    public Simples? simples { get; set; }
    public Simples? simei { get; set; }
}

class Registro
{
    public string IDListagem { get; set; } = "";
    public string CNPJ { get; set; } = "";
    public string SbjNum { get; set; } = "";
}

class Program
{
    static async Task Main()
    {
        string inputPath = "input.csv";
        string outputJsonDir = "ReceitaWS";
        string outputCsvPath = "IDListagem_Consolidado.csv";

        Directory.CreateDirectory(outputJsonDir);

        Console.WriteLine("Escolha uma opção:");
        Console.WriteLine("1 - Consultar CNPJs");
        Console.WriteLine("2 - Exportar CSV");
        var opcao = Console.ReadLine();

        var registros = File.ReadAllLines(inputPath)
            .Skip(1)
            .Select(l => l.Split(';'))
            .Where(cols => cols.Length >= 3)
            .Select(cols => new Registro
            {
                IDListagem = cols[0].Trim(),
                CNPJ = NormalizeCNPJ(cols[1]),
                SbjNum = cols[2].Trim()
            })
            .ToList();

        // Detectar e exibir duplicados
        var duplicados = registros
            .GroupBy(r => r.SbjNum)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicados.Any())
        {
            Console.WriteLine("⚠️ SbjNum duplicados encontrados:");
            foreach (var id in duplicados)
                Console.WriteLine($" - {id}");
        }

        // Remover duplicados
        registros = registros
            .GroupBy(r => r.SbjNum)
            .Select(g => g.First())
            .ToList();

        if (opcao == "1")
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ConsultaCNPJApp", "1.0"));

            foreach (var reg in registros)
            {
                var jsonPath = Path.Combine(outputJsonDir, $"IDListagem_{reg.IDListagem}.json");
                if (File.Exists(jsonPath)) continue;

                try
                {
                    var url = $"https://receitaws.com.br/v1/cnpj/{reg.CNPJ}";
                    var response = await httpClient.GetAsync(url);
                    var json = await response.Content.ReadAsStringAsync();

                    var empresa = JsonConvert.DeserializeObject<Empresa>(json);

                    if (empresa?.status?.ToUpper() == "OK")
                    {
                        await File.WriteAllTextAsync(jsonPath, json);
                        Console.WriteLine($"✅ CNPJ {reg.CNPJ} válido.");
                    }
                    else
                    {
                        await File.WriteAllTextAsync(jsonPath, "{}");
                        Console.WriteLine($"⚠️ CNPJ {reg.CNPJ} inválido.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro ao processar CNPJ {reg.CNPJ}: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(20));
            }
        }
        else if (opcao == "2")
        {
            var sb = new StringBuilder();
            sb.AppendLine("IDListagem;CNPJ;SbjNum;Nome;Tipo;Situacao;Porte;Natureza Juridica;Abertura;Logradouro;Numero;Bairro;Municipio;UF;CEP;Simples Optante;Simei Optante;Telefone;Capital Social;Status;Atividade Principal;Atividades Secundarias;Email");

            foreach (var reg in registros)
            {
                var jsonPath = Path.Combine(outputJsonDir, $"IDListagem_{reg.IDListagem}.json");
                if (!File.Exists(jsonPath))
                {
                    sb.AppendLine($"{reg.IDListagem};{reg.CNPJ};{reg.SbjNum};;;;;;;;;;;;;;;;;;;;;");
                    continue;
                }

                var json = File.ReadAllText(jsonPath);
                var empresa = JsonConvert.DeserializeObject<Empresa>(json);

                if (empresa?.status?.ToUpper() == "OK")
                {
                    string atividadePrincipal = string.Join(" | ",
                        empresa.atividade_principal?.Select(a => RemoverAcentos(a.texto ?? "")) ?? new List<string>());

                    string atividadesSecundarias = string.Join(" | ",
                        empresa.atividades_secundarias?.Select(a => RemoverAcentos(a.texto ?? "")) ?? new List<string>());

                    sb.AppendLine($"{reg.IDListagem};{reg.CNPJ};{reg.SbjNum};{Limpar(empresa.nome)};{Limpar(empresa.tipo)};{Limpar(empresa.situacao)};{Limpar(empresa.porte)};{Limpar(empresa.natureza_juridica)};{empresa.abertura};{Limpar(empresa.logradouro)};{empresa.numero};{Limpar(empresa.bairro)};{Limpar(empresa.municipio)};{empresa.uf};{empresa.cep};{empresa.simples?.optante};{empresa.simei?.optante};{empresa.telefone};{empresa.capital_social};{empresa.status};\"{atividadePrincipal}\";\"{atividadesSecundarias}\";{Limpar(empresa.email)}");
                }
                else
                {
                    sb.AppendLine($"{reg.IDListagem};{reg.CNPJ};{reg.SbjNum};;;;;;;;;;;;;;;;;;;;;");
                }
            }

            File.WriteAllText(outputCsvPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine("✅ CSV gerado com sucesso!");
        }
        else
        {
            Console.WriteLine("❌ Opção inválida.");
        }
    }

    static string NormalizeCNPJ(string rawCnpj)
    {
        var digitsOnly = new string(rawCnpj.Where(char.IsDigit).ToArray());
        return digitsOnly.PadLeft(14, '0');
    }

    static string RemoverAcentos(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var normalized = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    static string Limpar(string? texto)
    {
        return RemoverAcentos(texto ?? "").Replace("\"", "\"\"");
    }
}