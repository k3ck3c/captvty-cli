# Recherche avec jokers — Captvty CLI 2.9.3

## Origine

Pulsar a demandé la possibilité de sélectionner un épisode Arte avec des motifs, notamment dans `get_chaine_txt1_txt2.sh`. Une simple recherche `/6` pouvait sélectionner plusieurs épisodes d'Eldorado ; `*/6` doit être interprété comme un motif et non comme une sous-chaîne littérale.

## Syntaxe

| Motif | Signification |
|---|---|
| `Eldorado` | Sous-chaîne, comme auparavant |
| `Eldorado*` | Champ commençant par Eldorado |
| `*Eldorado*` | Champ contenant Eldorado |
| `Eldorado (?/6)` | Un caractère à la place de `?` |
| `\?` | Point d'interrogation littéral |
| `\*` | Astérisque littéral |
| `\\` | Barre oblique inverse littérale |

`*` accepte zéro ou plusieurs caractères ; `?` accepte exactement une unité UTF-16 après normalisation. Un motif contenant un joker non échappé porte sur le champ entier. Sans joker non échappé, la recherche reste une recherche de sous-chaîne, après interprétation des échappements. Une chaîne vide correspond à tout champ ; les restrictions propres aux commandes (par exemple info vide) restent applicables.

Les parenthèses, points, crochets et autres métacaractères des expressions régulières sont littéraux. Une barre oblique inverse devant un caractère autre que `*`, `?` ou `\` reste littérale, de même qu'une barre finale isolée. Les anciens motifs contenant un point d'interrogation ou un astérisque littéral doivent être adaptés.

Sous Bash, entourer les motifs d'apostrophes :

```bash
mono captvty-cli.exe list Arte 'Eldorado (?/6)'
mono captvty-cli.exe list Arte '*(3/6)'
mono captvty-cli.exe search --channel T18 'Face ? face'
mono captvty-cli.exe list Arte 'Film \?'
mono captvty-cli.exe get Arte 'Documentaire*Wolf Biermann*' '' high
```

Le dernier exemple a été validé par un téléchargement réel. Le sous-titre éditorial vide reste un argument obligatoire. Un script intermédiaire doit transmettre ses arguments entre guillemets (`"$1"`, `"$2"`, etc.) pour éviter l'expansion et le découpage par le shell.

## Champs comparés

La CLI et le provider partagent `text-matching.cs`. Le choix des champs reste propre à chaque commande :

| Commande | Champs |
|---|---|
| search dans le cache | Titre ou sous-titre |
| list provider classique | Titre ou sous-titre |
| info/get provider classique | Titre, sous-titre, episode_id, titre + ` - ` + sous-titre, titre + ` - ` + episode_id |
| list T18 | Titre, sous-titre ou concaténation séparée par une espace |
| search T18 | Titre ou sous-titre |
| info T18 | Titre ou sous-titre ; syntaxe composée `titre - sous-titre` également prise en charge |
| get T18 | Titre du programme et titre de l'épisode selon les deux arguments |
| list/info/get TV5MONDE+ | Titre de série ; sélection d'épisode distincte |

Les deux critères de get classique sont combinés par ET, chacun pouvant correspondre aux champs prévus par le provider. Les sélecteurs TV5MONDE+ `S1E2` ou `S1 E2` ne deviennent pas des motifs génériques. Le nom de chaîne et la recherche interne du TUI ne sont pas étendus aux jokers par cette modification.

## Implémentation

`NormalizeText` décompose en FormD, retire les caractères de catégorie NonSpacingMark, recompose en FormC puis applique ToLowerInvariant. `ContainsText` analyse les échappements et produit des tokens Literal, Star et Any. Sans joker, IndexOf avec comparaison ordinale conserve la recherche de sous-chaîne. Avec joker, `WildcardMatch` parcourt le champ normalisé, mémorise le dernier Star et reprend à cet emplacement si nécessaire. Il ne construit pas d'expression régulière.

La méthode Quote de la CLI est testée séparément pour vérifier la transmission des motifs au processus enfant Mono, y compris les espaces, guillemets, barres inverses et arguments vides.

## Validation réelle du 27 novembre 2026

### Arte

| Motif list | Résultats |
|---|---:|
| `Eldorado` | 12 |
| `Eldorado*` | 9 |
| `Eldorado (?/6)` | 6 |
| `*/6` | 0 |
| `*/6)` | 261 |
| `*(3/6)` | 43 |

Les six épisodes Eldorado (1/6) à (6/6) ont été identifiés. `info Arte 'Eldorado (3/?)'` retourne un épisode et six qualités. Pour get avec titre Eldorado, `*/6` retourne 4 (aucune correspondance), `*/6)` retourne 9 (six correspondances). Les motifs `*3/6)`, `*3/6*` et `*(3/?)` ont sélectionné uniquement Eldorado (3/6) dans un provider de diagnostic arrêté avant téléchargement.

Exemple pour le script de Pulsar, sous réserve qu'il transmette correctement les arguments :

```bash
./get_chaine_txt1_txt2.sh Arte Eldorado '*3/6*'
```

### T18

| Motif list | Avant correction des champs | Après |
|---|---:|---:|
| `Face à face` | 7 | 7 |
| `Face*` | 7 | 8 |
| `*à face` | 0 | 7 |
| `Risque*` | 0 | 1 |
| `*imminent` | 1 | 1 |
| `Face ? face` | 0 | 7 |
| `Face à face Risque imminent` | 1 | 1 |

La concaténation historique reste disponible, mais les champs individuels sont désormais également comparés dans list. Les essais search et info ont confirmé les motifs sur les champs individuels. get sans résultat ou ambigu retourne 3 pour T18. Les cinq sélections positives de get ont été vérifiées en diagnostic avant résolution du média.

### TV5MONDE+

`Un si grand soleil`, `Un si grand*` et `Un si grand sole?l` sélectionnent la même série (dix épisodes lors de l'essai). `info` et `get` avec `Un*` refusent les six séries correspondantes (code 9). Cette protection s'applique également aux recherches ambiguës sans joker. Sans résultat, info retourne 0 et get retourne 3, comme auparavant. Aucun téléchargement vidéo TV5MONDE+ n'a été effectué ; le périmètre reste le catalogue et les sous-titres.

## Tests automatisés

Depuis le dépôt :

```bash
CLI=/chemin/vers/captvty-cli.exe bash tests/test-text-matching.sh
```

| Suite | Debian 13 | Ubuntu 22.04.5 |
|---|---:|---:|
| test-text-matching.cs | 40/40 | 40/40 |
| test-text-matching-escape.cs | 16/16 | 16/16 |
| test-quote.cs | 10/10 | 10/10 |
| Total | 66/66 | 66/66 |

Le script accepte MONO et MCS pour sélectionner les outils et exige CLI. Il compile les tests dans un répertoire temporaire supprimé à la sortie. Les 66 tests sont inclus dans le bilan global de 124 tests par distribution.

Les résultats des catalogues sont des observations datées, non des nombres garantis. Toutes les chaînes et tous les motifs Unicode possibles n'ont pas été testés. Les caractères supplémentaires Unicode représentés par deux unités UTF-16 ne correspondent pas nécessairement à un seul `?`.
