# SpikeDce — Findings

**Date:** 2026-05-31 · **Issuer:** GMBIT LTDA (CNPJ 47712795000124, real e-CNPJ A1) · **Env:** SEFAZ PR/SVD homologação · **Stack:** .NET 8, xUnit

## Phase 0 — Manual issuance (GATE) → ✅ PASSED (exceeded)

A hand-built DCe for GMBIT was XSD- and WSDL-validated, enveloped-signed (rsa-sha1) with the real
e-CNPJ, submitted to `DCeAutorizacao` homologação, and **AUTHORIZED**.

**Evidence (live `retDCe`):**
```
cStat=100  xMotivo="Autorizado o uso do DCe"
chDCe=53260547712795000124990002647527991200744635
nProt=3532600000023909  digVal=3UVL6pGFuH7DtAF2FnnmC0kselw=  verAplic=PR-v1.0.0  cUF=41 (PR authorizer)
```

| # | Hypothesis | Verdict | Evidence |
|---|-----------|---------|----------|
| **H0** | Hand-built DCe is accepted/processed by the authorizer | ✅ **Confirmed (authorized)** | `cStat=100` after fixing only the homologação dest-name rule (the prior `cStat=598` already proved schema+signature were accepted) |
| **H4** | `infDCe` enveloped-signs (rsa-sha1, C14N, EndCertOnly, `#DCe`+chave) and self-verifies | ✅ Confirmed | `SignedXml.CheckSignature()` true; SEFAZ `digVal` returned; `Signed_dce_self_verifies_rsa_sha1` green |
| H1 (early) | Runtime `XmlSchemaSet` from real `dce/*.xsd` compiles + validates | ✅ Confirmed | `DceXsdValidator`/`DceWsdlValidator` compile + validate; 7/7 tests green |

### Key technical findings
- **Validation-first paid off.** Building XSD+WSDL validation before transport caught every structural gap locally (required `infDec`, `infSolicDCe`, `infDCeSupl`, and mandatory `ds:Signature`) — so the first live call already passed schema+signature and only hit a business rule.
- **`ds:Signature` is mandatory in `TDCe`** → an unsigned DCe cannot be XSD-valid; validation is done on the *signed* document.
- **WSDL `dceDadosMsg` is `<xs:any processContents="strict">`** → one `XmlSchemaSet` (WSDL schema + DC-e schemas) validates the inner `DCe` through the envelope. Proven real by an adversarial negative test.
- **Emissão Própria (`tpEmit=2`):** emitter CNPJ in `emit/CNPJ`; the `Fisco|Marketplace|Transportadora|ECT` choice is omitted.
- **cUF follows the real issuer UF** (GMBIT is registered in **DF → cUF=53**, not SP); the PR authorizer (cUF=41) accepts it.
- **Homologação rule:** destinatário `xNome` must be exactly `DCE EMITIDA EM AMBIENTE DE HOMOLOGACAO` (cStat 598 → 100).
- **.NET 8 cert load:** `X509CertificateLoader` is .NET 9+; on net8 use `new X509Certificate2(path, pwd, flags)`. The GMBIT A1 pfx (legacy PBE) loads fine and signs SHA-1.
- **Transport:** a minimal self-contained SOAP 1.2 + mTLS sender (`SefazDceClient`, string body) was sufficient; `Content-Type: application/soap+xml; action="..."`, no SOAP header. Status service returned `cStat=107`.

### Data sourcing (as built)
- `emit` = **real** GMBIT data by CNPJ (BrasilAPI fixture `assets/issuer/<cnpj>.json`, fetched once at dev time; no test-time HTTP).
- `dest` + items = **Bogus** synthetic (with the homologação dest-name override).
- Issuer identity, `.pfx` path + password = **env-driven**, nothing hardcoded.

## Phase 1 — Dynamic engine → ✅ PASSED

The hand-built XML was replaced by a **dynamic, schema-driven build** (`dict → SchemaModel → SoapEnvelopeBuilder`,
**zero per-DCe generated classes**), reusing Phase 0's exact signer + transport. The engine-built DCe was **authorized
live** with the same result as the hand-built one.

**Evidence (engine-built, live `retDCe`):** `cStat=100 "Autorizado o uso do DCe"`, nProt `3532600000023914`,
chDCe `…06838166`. Full suite: **12/12 green**.

| # | Hypothesis | Verdict | Evidence |
|---|-----------|---------|----------|
| **H1** | Runtime `XmlSchemaSet` compiles **and indexes** the `DCe`/`infDCe` tree | ✅ **Confirmed (now full)** | `SchemaModel.Load` compiles the set + resolves the global `DCe` element/particle tree (`SchemaModel_resolves_DCe_root_element`) |
| **H2** | Generic dict-driven `SoapEnvelopeBuilder` round-trips and **validates back**, zero codegen | ✅ **Confirmed** | `EngineBuilt_dce_signed_validates_xsd_and_wsdl` — engine output (signed) passes both XSD and the WSDL `<xs:any strict>` envelope, with **no per-DCe DTOs** (recursive `XmlSchemaParticle` walk over the compiled set) |
| **H4** (reuse) | Engine-built `infDCe` enveloped-signs + self-verifies | ✅ Confirmed | `EngineBuilt_dce_self_signs_and_verifies` — `CheckSignature()` true, `#DCe`+chave reference |
| **H5** | Signed bytes survive the transport hand-off byte-for-byte and still verify; same live `cStat` | ✅ **Confirmed (for the direct sender)** | `EngineBuilt_signed_bytes_survive_transport_and_verify` — the signed DCe appears **verbatim** in the captured POST body via `FakeEchoHandler` and re-verifies; live submit → `cStat=100` (same authorized result as Phase 0) |
| H3 | Complex-type inheritance | N/A | DCe XSDs contain no `complexContent`/`extension` |

### Phase 1 notes
- **No-recompile thesis demonstrated, not just asserted.** The builder walks the compiled particle tree (`Sequence`/
  `Choice`/`All`/`Element`, ref resolution, `minOccurs=0` omission, `maxOccurs>1` repetition, attribute `@name`,
  `InvariantCulture` coercion) — a SEFAZ XSD change is absorbed by reloading the schema, with no generated classes.
- **The signing seam stays fixed code** (per research): the engine builds the DOM, `DceSigner` signs `infDCe`, and the
  signed `DCe` is passed to the transport as a **string** (never re-walked) — byte-preservation confirmed by H5.
- **H5 caveat:** byte-preservation is proven for **our direct `SefazDceClient`** (string body). The original PRD H5 was
  specific to `AzTech.Net.Http.SoapClient`; vendoring that and re-running H5 against it is the one remaining fidelity item
  (the string-passthrough seam is identical in shape, so the risk is low).
- ⚠️ **Dep audit:** SDK 8.0.421's offline NuGet audit flags `System.Security.Cryptography.Xml` (even the latest 9.0.9) with
  NU1903 — appears to be a stale offline advisory DB; production must re-audit with a current SDK / patched package.

## Phase 2 — Canonical layer → ✅ PASSED

A canonical `DespatchAdvice` (UBL-Despatch-aligned) was converted to an authorized DC-e via the **declarative data
map** (`assets/mapping/dce_v1.00.map.json`), reusing the Phase-1 engine back-half with **zero code changes**.

**Evidence (canonical-mapped, live `retDCe`):** `cStat=100 "Autorizado o uso do DCe"`, nProt `3532600000023919`.

| # | Hypothesis | Verdict | Evidence |
|---|-----------|---------|----------|
| **H6** | A canonical `DespatchAdvice` → declarative map → existing engine → authorized live | ✅ **Confirmed** | `Canonical_mapped_dce_issues_against_homologacao` — `cStat=100`, nProt `3532600000023919`; full suite 20/20 green |

### Phase 2 notes
- **The canonical→DC-e mapping is data, not code.** `MappingEngine` is generic; a new national format requires only a
  new `*.map.json` (plus a new transform primitive only when a genuinely new computation is needed). The Phase-0/1
  engine back-half (`SchemaModel` / `SoapEnvelopeBuilder` / `DceSigner` / `SefazDceClient`) was **untouched**.
- **`infDCeSupl` QR base / `urlChave`** point to the PR portal (national DC-e authorizer) — correct for DC-e;
  these would be parameterized per-environment and per-state in production.
- Suite: **20/20 green**.

## Phase 3 (events) — unified verbs → ✅ PASSED

Issue and Cancel run through ONE `DfeEngine` instance; the verb/model difference is entirely resolved by the binding
registry + mapping data — **zero engine code changes** from Phase 2.

**Evidence (H7 — live `retDCe` + `retEventoDCe`):**
```
ISSUE  cStat=100  xMotivo="Autorizado o uso do DCe"      nProt=3532600000023986
CANCEL cStat=135  xMotivo="Cancelamento homologado"       nProt=3532600000023987
H7 CONFIRMED: evento registrado e vinculado
```

| # | Hypothesis | Verdict | Evidence |
|---|-----------|---------|----------|
| **H7** | Issue + Cancel through ONE `DfeEngine`, live SEFAZ homologação | ✅ **Confirmed (fully)** | ISSUE `cStat=100` authorized; CANCEL `cStat=135` "Cancelamento homologado" — event registered and linked to DCe; single engine instance, no branching |
| **H9** | `DfeEngine` has no model/verb branch (guard) | ✅ **Confirmed** | `H9_DfeEngine_has_no_model_or_verb_branch` — source scan finds zero hardcoded `"dce"/"issue"/"cancel"/"nfe"/"cte"` literals; Issue + Cancel share identical engine code paths, only binding+map data differ |

### Phase 3 notes
- **One engine, two verbs, zero branches.** `DfeEngine.Submit` is called identically for Issue and Cancel;
  the binding registry resolves `(model, verb) → (xsdDir, map, rootElement, serviceUrl, soapAction, wrapperNs, signedIdPath)`.
  No `if (verb == "cancel")` anywhere.
- **Component renames (during Phase 3 refactor):** `DceSigner` → `EnvelopedXmlSigner`,
  `SefazDceClient` → `SefazSoapClient`, `DceXsdValidator` → `XsdValidator`,
  `DceWsdlValidator` → `SoapEnvelopeXsdValidator`, `DceResult` → `SefazRetResult` — all
  made generic/reusable without changing any calling code in tests.
- **`SoapEnvelopeBuilder` xs:any support** added for events: the WSDL `<xs:any processContents="strict">`
  in `DCeRecepcaoEvento` is validated by passing the event XSD directory alongside the WSDL to
  `SoapEnvelopeXsdValidator`, the same one-`XmlSchemaSet` trick proven in Phase 0.
- **`cOrgao` must equal the access key's UF (same lesson as `cUF`); production should derive `cOrgao` from `chDCe[0:2]` rather than pass it as a caller-supplied value.**
- **Suite: 27/27 green.**

## Recommendation → **GO (all four phases confirmed)**
A real DCe issues and is **authorized** end-to-end in all phases — hand-built (Phase 0), dynamically
engine-built (Phase 1), canonical-layer-mapped (Phase 2), and with event verbs via the same engine (Phase 3).
All issue calls return `cStat=100`. The no-recompile thesis is **demonstrated**: schema is treated as data
(runtime `XmlSchemaSet` + particle walk, zero codegen), the canonical→DC-e translation is **data** (a
declarative `*.map.json`), and Issue/Cancel are **data-driven verbs** (different bindings + maps, same engine).
The fixed signing seam works on the real ICP-Brasil cert, and the signed bytes survive transport.
Proceed to **Gate 1 PRD/TRD** with the dynamic engine + canonical layer as the chosen design. Remaining
production-fidelity follow-ups (not blockers): (1) swap the direct sender for `AzTech.Net.Http.SoapClient` and re-run
H5 against it; (2) refresh the cert-xml dependency audit on a current SDK; (3) wire the signing key from
`taxpayers-certificates-api` and extend `tax-payers-api` with the DC-e registration type.
