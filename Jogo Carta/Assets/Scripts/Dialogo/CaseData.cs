using System;
using System.Collections.Generic;
using UnityEngine;

// ================================================================
// Estruturas de dados do caso. Espelham exatamente o JSON.
// JsonUtility exige que os nomes dos campos sejam IDÊNTICOS
// aos nomes usados no JSON (mesmo case).
// ================================================================

[Serializable]
public class CaseOption
{
    public string id;       // identificador único da opção (ex: "nome", "alibi")
    public string label;    // texto do botão (ex: "Qual é o seu nome?")
    [TextArea(2, 5)]
    public string response; // resposta que aparece ao clicar
}

[Serializable]
public class VerdictOptions
{
    public string condemnLabel = "Condenar";
    public string absolveLabel = "Absolver";
}

[Serializable]
public class CaseData
{
    public string caseId;                 // identificador único do caso
    public string caseName;                // nome exibido (opcional, útil pra debug/UI)
    public List<string> introLines;        // frases de contexto, exibidas em sequência
    public List<CaseOption> options;       // perguntas disponíveis (nome, álibi, etc)
    public string verdictText;             // texto mostrado antes do julgamento final
    public VerdictOptions verdictOptions;  // rótulos dos botões condenar/absolver
}
