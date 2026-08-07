# BCaT Architecture Validation

Generated: 2026-08-07 11:41 · mode: report

**0 error(s), 0 warning(s).** No error-severity rule is failing; the build gate passes.

## Summary by rule

| Rule | Severity | Failures | Title |
|---|---|---|---|
| BCAT-D003 | Warning | PASS | Every XRSimpleInteractable resolves to an interaction target |
| BCAT-D004 | Warning | PASS | Every XRSimpleInteractable is reachable by XRI casters |
| BCAT-D005 | Warning | PASS | No missing script references |
| BCAT-D006 | Warning | PASS | At most one AudioListener per platform branch |
| BCAT-L001 | Warning | PASS | No Quest-only components outside Platform/Quest |
| BCAT-L002 | Warning | PASS | No Desktop-only components outside Platform/Desktop |
| BCAT-L003 | Warning | PASS | Platform/ contains only rigs and platform services |
| BCAT-L004 | Warning | PASS | DevOnly subtrees are editor-only |
| BCAT-L005 | Warning | PASS | Raw platform APIs used only in sanctioned files |
| BCAT-L006 | Error | PASS | World-interaction keyboard polling is centralized |
| BCAT-P001 | Warning | PASS | Exactly one ScenePlatformBinding per inhabited scene |
| BCAT-P002 | Warning | PASS | Platform branches are authored inactive |
| BCAT-P003 | Warning | PASS | One root Platform group with Desktop/Quest children only |
| BCAT-P004 | Warning | PASS | One EventSystem per scene with exactly one input module |
| BCAT-P005 | Warning | PASS | One rig per kind, both under Platform/ |
| BCAT-P006 | Warning | PASS | One XRInteractionManager, under Platform/Quest |
| BCAT-Q001 | Warning | PASS | Trigger-only interaction targets carry an XR select surface |
| BCAT-Q002 | Warning | PASS | Both desktop and XR prompts are valid |
| BCAT-S001 | Error | PASS | Transition destination scenes are loadable |
| BCAT-S002 | Error | PASS | Transition spawn ids resolve |
| BCAT-S003 | Warning | PASS | Each platform branch has a MainCamera |
| BCAT-S004 | Warning | PASS | Presentation scenes are head-tracked on Quest |
| BCAT-S005 | Error | PASS | Quality tiers exist with the expected names |
| BCAT-S006 | Error | PASS | Black Kitchen Addressables group uses local paths |
| BCAT-S007 | Error | PASS | Android application identifier is project-owned |

