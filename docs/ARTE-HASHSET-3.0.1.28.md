# Protection du HashSet concurrent du provider Arte

## Contexte

- Captvty : 3.0.1.28
- Captvty CLI : 2.9.1
- Système : Debian 13
- Environnement : Mono
- Date : 24 novembre 2026
- Commit : dd2e090

## 1. Symptôme initial

Lors d'une tentative de téléchargement de Jossi (2/6)
sur Arte, le processus s'est terminé avec le code 255.

L'exception est survenue avant le début effectif
du téléchargement.

Extrait de la pile d'appels :

~~~text
System.IndexOutOfRangeException
System.Collections.Generic.HashSet`1[T].AddIfNotPresent
System.Collections.Generic.HashSet`1[T].Add
_Xfb._0gB(JToken, _vvb, ISet`1)
_Xfb+_c3A._oG(JToken)
_Xfb._p3B()
_x9A._thb()
AsyncMethodBuilderCore.<ThrowAsync>m__1
~~~

Une nouvelle tentative a réussi sans modification
du moteur.

Le caractère intermittent de cette erreur est compatible
avec un problème d'accès concurrent.

## 2. Précédent historique : BFMTV

Un problème similaire avait déjà été rencontré
avec Captvty 3.0.1.26.

Le correctif correspondant est conservé dans :

~~~text
patch-bfmtv-hashset.cs
~~~

Ce patch avait été intégré à Captvty CLI 2.5.0,
dans le commit 7e765c0.

Il protégeait un appel à HashSet.Add dans :

~~~text
_CmA/_MBb/_Dib::MoveNext
~~~

La protection utilisait Monitor.Enter et Monitor.Exit,
avec libération du verrou dans un bloc finally.

Cette expérience a orienté l'analyse du problème Arte.

## 3. Outils d'analyse utilisés

Les outils suivants appartiennent à captvty-lab :

| Outil | Fonction |
|---|---|
| dump-type.exe | Examiner les champs et méthodes d'un type |
| dump-method-il.exe | Examiner les instructions IL d'une méthode |
| find-field-refs.exe | Rechercher les références à un champ |
| find-callers.exe | Rechercher les appels à une méthode |
| compare-il.exe | Comparer deux assemblies méthode par méthode |

Ces outils utilisent Mono.Cecil.

Préparation des variables sous Bash :

~~~bash
LAB="$HOME/captvty-lab/tools"
TARGET="$HOME/Téléchargements/captvty-3.0.1.28"

ENGINE="$TARGET/Captvty-cli-engine-28-progress.exe"

TOOL="$LAB/dump-type.exe"
DUMP="$LAB/dump-method-il.exe"
REFS="$LAB/find-field-refs.exe"
CALLERS="$LAB/find-callers.exe"
~~~

Pour analyser le moteur avant application du patch
HashSet, utiliser une copie antérieure au troisième
patch ou reconstruire le moteur intermédiaire.

## 4. Identification des méthodes

Examen du type principal :

~~~bash
mono "$TOOL" "$ENGINE" '_Xfb'
~~~

Méthodes identifiées :

~~~text
_Xfb::_p3B()
_Xfb::_62b(JToken, ISet<string>)
_Xfb::_0gB(JToken, _vvb, ISet<string>)
~~~

Examen du type imbriqué :

~~~bash
mono "$TOOL" "$ENGINE" '_Xfb/_c3A'
~~~

Champs importants :

~~~text
_JEA : _Xfb
_PgA : HashSet<string>
~~~

Méthode asynchrone :

~~~text
_Xfb/_c3A::_oG(JToken)
~~~

Machines d'état identifiées :

| Méthode | Machine d'état |
|---|---|
| _Xfb::_p3B | _Xfb/_KNb |
| _Xfb/_c3A::_oG | _Xfb/_c3A/_6NA |
| _Xfb::_62b | _Xfb/_oob |

## 5. Analyse des instructions IL

Extraction des quatre méthodes importantes :

~~~bash
mono "$DUMP" "$ENGINE" '_Xfb' '_0gB' \
    > /tmp/Xfb-0gB-28.txt

mono "$DUMP" "$ENGINE" '_Xfb/_KNb' MoveNext \
    > /tmp/Xfb-KNb-MoveNext-28.txt

mono "$DUMP" "$ENGINE" '_Xfb/_c3A/_6NA' MoveNext \
    > /tmp/Xfb-6NA-MoveNext-28.txt

mono "$DUMP" "$ENGINE" '_Xfb/_oob' MoveNext \
    > /tmp/Xfb-oob-MoveNext-28.txt
~~~

Recherche des insertions dans le HashSet :

~~~bash
grep -n -B8 -A8 '::Add(' /tmp/Xfb-0gB-28.txt

grep -n -B8 -A8 '::Add(' \
    /tmp/Xfb-oob-MoveNext-28.txt
~~~

Deux sites d'insertion ont été identifiés.

### Premier site

~~~text
_Xfb::_0gB
IL_00F7 : callvirt ISet<string>::Add
~~~

### Second site

~~~text
_Xfb/_oob::MoveNext
IL_00E1 : callvirt ISet<string>::Add
~~~

Le second site reçoit le même ensemble via
le paramètre transmis à _62b.

## 6. Mise en évidence de la concurrence

Les références au champ _PgA ont permis de suivre
la création et l'utilisation du HashSet.

| Méthode | Offset IL | Opération |
|---|---|---|
| _KNb::MoveNext | 0091 | Création du HashSet |
| _KNb::MoveNext | 0096 | Stockage dans _PgA |
| _6NA::MoveNext | 0456 | Lecture de _PgA |
| _6NA::MoveNext | 045B | Appel de _0gB |
| _6NA::MoveNext | 0828 | Lecture de _PgA |
| _6NA::MoveNext | 082D | Appel de _0gB |
| _KNb::MoveNext | 0669 | Lecture de _PgA |
| _KNb::MoveNext | 066E | Appel de _62b |

Recherche des opérations asynchrones :

~~~bash
grep -n -B8 -A8 -E \
    'WhenAll|Enumerable::Select|::_oG' \
    /tmp/Xfb-KNb-MoveNext-28.txt
~~~

Séquence IL significative :

~~~text
IL_0257 : ldftn _Xfb/_c3A::_oG
IL_025D : newobj Func
IL_0262 : Enumerable.Select<JToken,Task>
IL_0267 : Task.WhenAll(IEnumerable<Task>)
IL_026D : ConfigureAwait(false)
~~~

Les tâches peuvent donc progresser concurremment
tout en utilisant le même HashSet.

HashSet<T> ne garantit pas la sécurité des écritures
concurrentes sans synchronisation externe.

La pile d'exception et l'analyse IL constituent
des indices concordants d'une course entre insertions.

## 7. Principe du correctif

Source :

~~~text
patch-arte-hashset-28.cs
~~~

Le patch ajoute une méthode statique :

~~~text
_Xfb::_LockedAdd(ISet<string>, string) : Boolean
~~~

Son comportement est équivalent au code C# suivant :

~~~csharp
static bool LockedAdd(ISet<string> set, string value)
{
    lock (set)
    {
        return set.Add(value);
    }
}
~~~

Le verrou porte directement sur l'ensemble partagé.

Le code IL généré utilise :

- Monitor.Enter ;
- ISet<string>.Add ;
- Monitor.Exit dans un bloc finally.

Les deux appels callvirt d'origine sont remplacés
par des appels call vers la nouvelle méthode statique.

La valeur booléenne retournée par Add est conservée.

Le patch ne remplace pas le HashSet par une autre
structure de données.

## 8. Précautions prises par le patch

Avant de produire le moteur modifié, le patch vérifie :

- la présence du type _Xfb ;
- la présence de la méthode _0gB ;
- la présence du type imbriqué _oob ;
- la présence de sa méthode MoveNext ;
- l'unicité de chaque appel ISet<string>.Add recherché ;
- les offsets IL attendus : 00F7 et 00E1.

Le moteur de destination n'est écrit qu'après
validation des deux sites.

Ces contrôles évitent d'appliquer silencieusement
le patch à une structure IL inattendue.

## 9. Compilation manuelle

Depuis le répertoire du dépôt :

~~~bash
CECIL=$(find /usr/lib/mono/gac/Mono.Cecil \
    -name Mono.Cecil.dll |
    sort -V |
    tail -1)

test -f "$CECIL" &&
    mcs -r:"$CECIL" \
        -out:/tmp/patch-arte-hashset-28.exe \
        patch-arte-hashset-28.cs
~~~

Application sur un moteur intermédiaire non encore
protégé contre les accès concurrents :

~~~bash
mono /tmp/patch-arte-hashset-28.exe \
    Captvty-cli-engine-28-progress-tmp.exe \
    /tmp/Captvty-cli-engine-28-hashset.exe
~~~

L'installateur effectue automatiquement cette opération.

## 10. Intégration dans install.sh

Ordre d'application des patches :

~~~text
Captvty.exe original
    |
    | patch-dpi-28
    v
Captvty-cli-engine-28.exe
    |
    | patch-28-progress
    v
Captvty-cli-engine-28-progress-tmp.exe
    |
    | patch-arte-hashset-28
    v
Captvty-cli-engine-28-progress.exe
~~~

Le nom du moteur final est conservé afin de ne pas
modifier les références existantes dans la CLI
et le provider.

## 11. Vérification du moteur final

~~~bash
mono "$LAB/compare-il.exe" \
    "$TARGET/Captvty.exe" \
    "$TARGET/Captvty-cli-engine-28-progress.exe" \
    --details
~~~

Résultat obtenu :

~~~text
DIFF    _qh/_X4B::_a0():System.Void  [4 / 8]
DIFF    _qh/_X4B::_qJB(System.Double):System.Void  [6 / 10]
DIFF    _RMb::_Srb(System.Drawing.Image,System.Int32,System.Int32):System.Drawing.Bitmap  [142 / 142]
DIFF    _Xfb/_oob::MoveNext():System.Void  [582 / 582]
DIFF    _Xfb::_0gB(Newtonsoft.Json.Linq.JToken,_vvb,System.Collections.Generic.ISet`1<System.String>):System.Void  [307 / 307]
ONLY-B  _Xfb::_LockedAdd(System.Collections.Generic.ISet`1<System.String>,System.String):System.Boolean
SUMMARY different=6 same=8440 onlyA=0 onlyB=1
~~~

Le compteur different inclut les méthodes ajoutées
et supprimées.

Bilan :

| Caractéristique | Nombre |
|---|---:|
| Méthodes originales | 8445 |
| Méthodes inchangées selon le comparateur | 8440 |
| Méthodes modifiées | 5 |
| Méthodes ajoutées | 1 |
| Méthodes supprimées | 0 |
| Méthodes finales | 8446 |

99,94 % des méthodes originales sont considérées
comme inchangées par compare-il.

Ce résultat ne constitue pas une comparaison
binaire exhaustive des deux assemblies.

## 12. Test de concurrence indépendant

Source :

~~~text
tests/test-hashset-28.cs
~~~

Compilation :

~~~bash
mcs -out:/tmp/test-hashset-28.exe \
    tests/test-hashset-28.cs
~~~

Exécution :

~~~bash
cd "$TARGET"

MONO_PATH="$PWD:$PWD/bin:$PWD/bin/cefsharp" \
    mono /tmp/test-hashset-28.exe \
    "$PWD/Captvty-cli-engine-28-progress.exe"
~~~

Le test appelle directement la méthode ajoutée
au moteur par l'intermédiaire de la réflexion.

Il effectue cinq séries de tests.

Pour chaque série :

- huit travailleurs logiques avec Parallel.For ;
- 50 000 valeurs distinctes par travailleur ;
- insertion de chaque valeur ;
- nouvelle insertion de la même valeur ;
- vérification du résultat booléen ;
- vérification du nombre final d'éléments.

Chaque série représente :

- 400 000 insertions uniques ;
- 400 000 tentatives d'insertion de doublons ;
- 800 000 appels à _LockedAdd.

Les cinq séries représentent quatre millions d'appels.

Résultats obtenus avec le moteur final de la 2.9.1 :

| Série | Éléments distincts | Durée |
|---|---:|---:|
| 1 | 400000 | 1646 ms |
| 2 | 400000 | 1624 ms |
| 3 | 400000 | 1746 ms |
| 4 | 400000 | 1717 ms |
| 5 | 400000 | 1813 ms |

Bilan : cinq séries réussies, aucune erreur.

## 13. Validation avec Arte

Cinq mises à jour consécutives du catalogue Arte
ont également été réalisées après application du patch.

| Essai | Entrées Arte | Durée | Code retour |
|---|---:|---:|---:|
| 1 | 14306 | 10,655 s | 0 |
| 2 | 14306 | 10,462 s | 0 |
| 3 | 14306 | 10,128 s | 0 |
| 4 | 14306 | 9,511 s | 0 |
| 5 | 14306 | 11,400 s | 0 |

Le catalogue global contenait 14510 entrées.

Des téléchargements réels ont également été réalisés,
notamment Jossi (5/6) et Jossi (6/6).

Après installation définitive de la version 2.9.1,
une nouvelle mise à jour Arte a réussi :

~~~text
Arte : 14310 entrées
Catalogue global : 14515 entrées
Code retour : 0
~~~

Les différences de nombre d'entrées correspondent
à des relevés effectués à des moments différents.

## 14. Limites de la validation

Le défaut initial était intermittent.

L'absence d'erreur pendant les tests ne démontre pas
que toutes les courses possibles ont été éliminées.

Le correctif protège les deux sites d'insertion
identifiés dans cette version du provider Arte.

Il ne constitue pas une synchronisation générale
de toutes les collections du moteur Captvty.

Les noms obfusqués et les offsets IL peuvent changer
dans une future version de Captvty.

Une nouvelle analyse sera alors nécessaire.

## 15. Fichiers associés

~~~text
patch-arte-hashset-28.cs
tests/test-hashset-28.cs
install.sh
~~~

Voir également :

- [README principal](../README.md)
- [Construction de Captvty 3.0.1.26](BUILD-3.0.1.26.md)
- [Résolution du chemin final](FINAL-DOWNLOAD-PATH.md)
