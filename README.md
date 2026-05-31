# SpikeDce

Throwaway spike: prove (or kill) issuing a Brazilian **DC-e** (Declaração de Conteúdo eletrônica)
end-to-end — hand-built XML first (Phase 0), then a dynamic SOAP-from-XSD engine (Phase 1) — against
SEFAZ **PR/SVD homologação**.

## Environment variables

**Required:**
- `DCE_SPIKE_PFX_PATH` — absolute path to the issuer e-CNPJ **A1** `.pfx`
- `DCE_SPIKE_PFX_PASSWORD` — its password
- `DCE_SPIKE_CNPJ` — 14-digit emitter CNPJ

**Optional:**
- `DCE_SPIKE_XNOME` — override the looked-up razão social
- `DCE_SPIKE_CUF` (default `35` = SP), `DCE_SPIKE_UF` (default `SP`)
- `DCE_SPIKE_SKIP_SEFAZ` — set to skip the live SEFAZ tests

> Nothing about the issuer, cert path, or password is hardcoded in source. The `.pfx` is `.gitignore`d.

## Dev-time setup (issuer data)

Fetch the issuer company data once and commit it as a fixture (no HTTP runs during tests):

```bash
mkdir -p assets/issuer
curl -fsS "https://brasilapi.com.br/api/cnpj/v1/${DCE_SPIKE_CNPJ}" -o "assets/issuer/${DCE_SPIKE_CNPJ}.json"
```

## Run

```bash
dotnet test                                       # all (32 tests; Phase 0 + Phase 1 + Phase 2 + Phase 3)
dotnet test --filter HandBuilt_dce_validates      # Phase 0 validation-first gate (XSD + WSDL)
dotnet test --filter Phase0                        # hand-built path incl. live authorize
dotnet test --filter Phase1                        # dynamic engine (SchemaModel/SoapEnvelopeBuilder) incl. live authorize
dotnet test --filter Phase2                        # canonical DespatchAdvice JSON -> declarative map -> authorized DCe
dotnet test --filter Phase3                        # generic DFe engine: Issue + Cancel verbs through one binding-driven DfeEngine
dotnet test --filter Phase3_CodeTable              # code-table registry: XSD-validated membership + versioned lookup
```

> Reference tables (`cClassTrib`, `cCredPres`, `meiosPagamento`) validate via **generated `<xs:enumeration>` XSDs** (one per table version) + a **versioned `CodeTableRegistry`** for enrichment and date-based version selection.

**Result:** a real GMBIT DCe is **authorized** by SEFAZ PR homologação (`cStat=100`) hand-built (Phase 0),
dynamically engine-built (Phase 1), via the canonical layer (Phase 2 — UBL-aligned `DespatchAdvice` JSON →
declarative `dce_v1.00.map.json` → authorized DC-e, no code changes to the engine), via unified Issue + Cancel
verbs through one binding-driven `DfeEngine` (Phase 3), and with reference-table membership + enrichment via a
versioned `CodeTableRegistry` with XSD-validated code sets (Phase 3 H8). See `FINDINGS.md`.
