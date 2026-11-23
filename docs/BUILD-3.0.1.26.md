# Construction historique avec Captvty 3.0.1.26

Documentation des versions antérieures à Captvty CLI 2.9.0.

Cette procédure ne doit pas être utilisée avec Captvty 3.0.1.28.

## Prérequis

- Linux
- Mono
- `mcs`
- Mono.Cecil
- `jq` (optionnel, pour interroger directement `captvty-cache.json`)
- une installation originale de **Captvty 3.0.1.26**

Sous Debian, les dépendances nécessaires peuvent être installées par exemple avec :

```bash
sudo apt install mono-devel libmono-cecil-private-cil
```

Pour installer également `jq` :

```bash
sudo apt install jq
```

Mono.Cecil peut être installé dans une version différente suivant la version de Debian.

Le chemin peut être déterminé automatiquement :

```bash
CECIL=$(find /usr/lib/mono/gac/Mono.Cecil \
  -name Mono.Cecil.dll \
  | sort -V \
  | tail -1)

echo "$CECIL"
```

Par exemple, le test sur Debian 12 utilise Mono.Cecil `0.11.0.0`, alors qu'une installation Debian 13 peut disposer de `0.11.1.0`.

## Installation rapide

Captvty 3.0.1.26 doit déjà être présent sur la machine.

Cloner le dépôt :

```bash
git clone https://github.com/k3ck3c/captvty-cli.git
cd captvty-cli
```

Puis lancer :

```bash
./install.sh
```

Le script demande le répertoire contenant l'installation originale de
Captvty 3.0.1.26. Il vérifie notamment la présence de `Captvty.exe`,
`Captvty.exe.config` et `bin/`, ainsi que Mono, `mcs` et Mono.Cecil.

Il reconstruit ensuite Captvty CLI dans un répertoire temporaire, vérifie
le binaire obtenu, sauvegarde les éventuels fichiers d'une installation
précédente, puis installe Captvty CLI dans le répertoire choisi.

Pour la version 2.7.0, `install.sh` prépare également un environnement Python
dédié contenant `yt-dlp`, utilisé notamment pour le téléchargement des replays T18.

Le répertoire peut également être indiqué directement :

```bash
./install.sh /chemin/vers/Captvty
```

## Construction depuis les sources

Le dépôt ne fournit pas `Captvty.exe`.

Après avoir cloné le dépôt, décompressez Captvty 3.0.1.26 Windows original directement dans le répertoire du clone.

Par exemple :
```bash
git clone https://github.com/k3ck3c/captvty-cli.git
cd captvty-cli
unzip /chemin/vers/captvty-3.0.1.26.zip
```

Après extraction, le répertoire doit notamment contenir :

Captvty.exe

Captvty.exe.config

bin/

Vidéos/


Le répertoire `Vidéos/`, créé lors de l'extraction de Captvty, est utilisé pour accueillir les émissions téléchargées.


Le fichier d'entrée doit être le `Captvty.exe` **Windows original de Captvty 3.0.1.26**. N'utilisez pas une version déjà modifiée ou adaptée pour Mono.

Les patches Cecil utilisent certains noms internes obfusqués de Captvty 3.0.1.26. Ils ne sont donc pas garantis compatibles avec une autre version de Captvty.

Les autres fichiers de l'installation originale, notamment le répertoire `bin/`, doivent être conservés pour l'exécution de la CLI.

La chaîne de construction du moteur est la suivante :

```text
Captvty.exe
    │
    │ patch-mono-compat
    ▼
Captvty-mono-clean.exe
    │
    │ patch-captvty-cli
    ▼
Captvty-cli-engine-unpatched.exe
    │
    │ patch-bfmtv-hashset
    ▼
Captvty-cli-engine.exe
    │
    │ patch-m6
    ▼
Captvty-cli-engine-m6.exe
```

`Captvty-cli-engine.exe` est le moteur utilisé par défaut par la CLI.

### 1. Déterminer le chemin de Mono.Cecil

```bash
CECIL=$(find /usr/lib/mono/gac/Mono.Cecil \
  -name Mono.Cecil.dll \
  | sort -V \
  | tail -1)

echo "$CECIL"
```

### 2. Compiler les outils de patch

Compiler le patch de compatibilité Mono :

```bash
mcs \
  -r:$CECIL \
  -r:System.Windows.Forms \
  -r:System.Drawing \
  -out:patch-mono-compat.exe \
  patch-mono-compat.cs
```

Compiler le patch du moteur CLI :

```bash
mcs \
  -r:$CECIL \
  -out:patch-captvty-cli.exe \
  patch-captvty-cli.cs
```

Compiler le patch M6 :

```bash
mcs \
  -r:$CECIL \
  -out:patch-m6.exe \
  patch-m6.cs
```

Compiler le correctif BFMTV :

```bash
mcs \
  -r:$CECIL \
  -out:patch-bfmtv-hashset.exe \
  patch-bfmtv-hashset.cs
```

### 3. Construire le moteur compatible Mono

À partir du `Captvty.exe` Windows original de Captvty 3.0.1.26 :

```bash
mono patch-mono-compat.exe \
  Captvty.exe \
  Captvty-mono-clean.exe
```

Le programme doit notamment afficher :

```text
Créé : Captvty-mono-clean.exe
```

Construire ensuite le moteur utilisé par la CLI :

```bash
mono patch-captvty-cli.exe \
  Captvty-mono-clean.exe \
  Captvty-cli-engine-unpatched.exe
```

Le résultat attendu comprend notamment :

```text
Patch : System.Void _exB::_GUA(_8eB)
Patch : System.Void _exB::_XFb()
OK : moteur CLI écrit dans Captvty-cli-engine-unpatched.exe
```

Appliquer ensuite le correctif BFMTV :

```bash
mono patch-bfmtv-hashset.exe \
  Captvty-cli-engine-unpatched.exe \
  Captvty-cli-engine.exe
```

Le résultat attendu comprend notamment :

```text
patched=1
written: Captvty-cli-engine.exe
```

`patched=1` confirme que le correctif attendu a été appliqué exactement une fois.

`Captvty-cli-engine.exe` est alors le moteur utilisé par défaut par la CLI.

Le patch M6 peut ensuite être appliqué si nécessaire :

```bash
mono patch-m6.exe \
  Captvty-cli-engine.exe \
  Captvty-cli-engine-m6.exe
```

Il produit une variante :

```text
Captvty-cli-engine-m6.exe
```

La CLI utilise par défaut `Captvty-cli-engine.exe`.

### 4. Compiler le provider

```bash
mcs \
  -r:System.Web.Extensions \
  -out:captvty-provider.exe \
  captvty-provider.cs
```

### 5. Compiler les shims CefSharp

```bash
mcs -target:library \
  -out:CefSharp.dll \
  CefSharp-stub.cs

mcs -target:library \
  -out:CefSharp.Core.dll \
  -r:CefSharp.dll \
  -r:System.Net.Http.dll \
  CefSharp-Core-stub.cs

mcs -target:library \
  -out:CefSharp.WinForms.dll \
  -r:CefSharp.dll \
  -r:CefSharp.Core.dll \
  CefSharp-WinForms-stub.cs
```

Le warning `CS0067` concernant JavascriptObjectRepository.ResolveObject peut être ignoré.

Les trois DLL produites doivent rester dans le répertoire courant, à côté de captvty-cli.exe et captvty-provider.exe.

### 6. Compiler la CLI

```bash
mcs \
  -r:$CECIL \
  -out:captvty-cli.exe \
  -r:System.Web.Extensions \
  captvty-cli-v2.8.0.cs
```

À l'issue de la compilation, le répertoire d'exécution contient notamment :

```text
Captvty.exe
Captvty-cli-engine.exe
Captvty-cli-engine-m6.exe
captvty-provider.exe
captvty-cli.exe
CefSharp.dll
CefSharp.Core.dll
CefSharp.WinForms.dll
bin/
```


Les autres DLL et fichiers nécessaires à Captvty doivent rester présents dans son installation.

### Validation de la construction

La chaîne complète de construction a été testée depuis un clone propre du dépôt sur **Debian 12**, à partir du `Captvty.exe` Windows original de Captvty 3.0.1.26.


Par exemple :

```bash
mono captvty-cli.exe --timeout 60 list "France 2" "journal"
```
