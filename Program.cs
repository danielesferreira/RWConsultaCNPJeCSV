// See https://aka.ms/new-console-template for more information
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

class Program
{
    static async Task Main()
    {
        string inputPath = "input.csv";
        string outputJsonDir = "ReceitaWS";
        string outputCsvPath = "ReceitaWS_Consolidado.csv";

        Directory.CreateDirectory(outputJsonDir);

        Console.WriteLine("Escolha uma opção:");
        Console.WriteLine("1 - Consultar CNPJs");
        Console.WriteLine("2 - Exportar CSV");
        var opcao = Console.ReadLine();

        if (opcao == "1")
        {
            var rawCnpjs = File.ReadAllLines(inputPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(NormalizeCNPJ)
                .Distinct()
                .ToList();

            var arquivosExistentes = Directory.GetFiles(outputJsonDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f)?.Replace("ReceitaWS_", ""))
                .ToHashSet();

            var novosCnpjs = rawCnpjs.Where(c => !arquivosExistentes.Contains(c)).ToList();

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ConsultaCNPJApp", "1.0"));

            foreach (var cnpj in novosCnpjs)
            {
                try
                {
                    var url = $"https://receitaws.com.br/v1/cnpj/{cnpj}";
                    var response = await httpClient.GetAsync(url);
                    var json = await response.Content.ReadAsStringAsync();

                    var empresa = JsonConvert.DeserializeObject<Empresa>(json);

                    if (empresa?.status?.ToUpper() == "OK")
                    {
                        var jsonPath = Path.Combine(outputJsonDir, $"ReceitaWS_{cnpj}.json");
                        await File.WriteAllTextAsync(jsonPath, json);
                        Console.WriteLine($"✅ CNPJ {cnpj} salvo.");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ CNPJ {cnpj} inválido ou não encontrado.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro ao processar CNPJ {cnpj}: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(20)); // Limite de 3 consultas por minuto
            }
        }
        else if (opcao == "2")
        {
            var sb = new StringBuilder();
            if (!File.Exists(outputCsvPath))
            {
                sb.AppendLine("CNPJ,Nome,Tipo,Situacao,Porte,Natureza Juridica,Abertura,Logradouro,Numero,Bairro,Municipio,UF,CEP,Simples Optante,Simei Optante,Telefone,Capital Social,Status,Atividade Principal,Atividades Secundarias");
            }

            var arquivosJson = Directory.GetFiles(outputJsonDir, "*.json");
            var cnpjsExistentes = File.Exists(outputCsvPath)
                ? File.ReadAllLines(outputCsvPath).Skip(1).Select(l => l.Split(',')[0]).ToHashSet()
                : new HashSet<string>();

            foreach (var arquivo in arquivosJson)
            {
                string json = File.ReadAllText(arquivo);
                var empresa = JsonConvert.DeserializeObject<Empresa>(json);
                if (empresa != null && empresa.status?.ToUpper() == "OK" && !cnpjsExistentes.Contains(empresa.cnpj))
                {
                    string atividadePrincipal = string.Join(" | ",
                        empresa.atividade_principal?.Select(a => RemoverAcentos(a.texto ?? "")) ?? new List<string>());

                    string atividadesSecundarias = string.Join(" | ",
                        empresa.atividades_secundarias?.Select(a => RemoverAcentos(a.texto ?? "")) ?? new List<string>());

                    sb.AppendLine($"{NormalizeCNPJ(empresa.cnpj ?? "")},{Limpar(empresa.nome)},{Limpar(empresa.tipo)},{Limpar(empresa.situacao)},{Limpar(empresa.porte)},{Limpar(empresa.natureza_juridica)},{empresa.abertura},{Limpar(empresa.logradouro)},{empresa.numero},{Limpar(empresa.bairro)},{Limpar(empresa.municipio)},{empresa.uf},{empresa.cep},{empresa.simples?.optante},{empresa.simei?.optante},{empresa.telefone},{empresa.capital_social},{empresa.status},\"{atividadePrincipal}\",\"{atividadesSecundarias}\"");
                }
            }

            File.AppendAllText(outputCsvPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine("✅ CSV atualizado com sucesso!");
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