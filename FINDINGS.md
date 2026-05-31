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

## Deferred / not yet done
- **AzTech.Net.Http `SoapClient` reuse + H5 (byte-preservation through SoapClient)** — Phase 0 used a direct sender (string passthrough preserves bytes trivially, and authorization succeeded, so the signing seam is proven viable). Vendoring AzTech + the H5 byte-capture test remain for Phase 1.
- **Phase 1 (dynamic engine: `SchemaModel` + `SoapEnvelopeBuilder`, H1/H2/H5)** — not started; foundation (validators, signer, transport, real authorization) is in place to build on.

## Recommendation → **GO**
A real DCe issues and is **authorized** end-to-end with the dynamic-validation + signing + SOAP path.
The no-recompile thesis is well-supported for Phase 1: schema is already treated as data (runtime
`XmlSchemaSet`), the signing seam works on the real cert, and the transport is a thin string-passthrough.
Proceed to Phase 1 (dynamic `SoapEnvelopeBuilder`) and, for production fidelity, swap in the AzTech
`SoapClient` and add the H5 byte-preservation test.
