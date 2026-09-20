# FoundMySave — RuneScape: Dragonwilds

Retrouve vos mondes Dragonwilds sur votre PC, **y compris ceux du Game Pass**, et les
prépare pour un serveur dédié.

Sur Steam, vos mondes sont des fichiers `.sav` rangés dans un dossier normal. Sur le
Game Pass, c'est une autre histoire : ils sont **compressés**, **renommés en
identifiants illisibles**, **sans extension**, dans un dossier que **la recherche
Windows n'indexe pas**. Quatre obstacles qui font qu'aucune recherche classique ne
trouve jamais rien, quel que soit le nom tapé.

FoundMySave les retrouve quand même, en identifiant les fichiers par leur contenu
plutôt que par leur nom.

## Ce que ça fait

- Détecte les installations **Steam** et **Game Pass**
- Reconnaît les mondes à leur signature interne, pas à leur nom de fichier
- **Décompresse** les mondes du Game Pass
- Lit le **nom du monde** et la **date de sauvegarde** à l'intérieur du fichier
- Écarte les sauvegardes de secours et ne garde que la version la plus récente
- Exporte sous le bon nom, prêt à l'emploi
- Prépare un dossier complet pour un **serveur dédié**, avec la marche à suivre
- En option, garde une copie horodatée avant une mise à jour du jeu

Vos fichiers d'origine ne sont **jamais modifiés**. L'outil lit et copie, rien d'autre.

## Installation

Téléchargez `FoundMySave.exe` depuis la page
[Releases](https://github.com/KaineOfficial/FoundMySave/releases) et lancez-le.

Rien à installer, aucune dépendance, pas besoin des droits administrateur.

> Windows peut afficher un avertissement « Windows a protégé votre ordinateur » sur un
> programme peu téléchargé. Cliquez sur **Informations complémentaires** puis
> **Exécuter quand même**. Le code de cet outil est entièrement lisible ici.

## Où sont les sauvegardes

| Installation | Emplacement | Format |
|---|---|---|
| Steam | `%LOCALAPPDATA%\RSDragonwilds\Saved\SaveGames\` | `.sav` en clair, nommés d'après le monde |
| Game Pass | `%LOCALAPPDATA%\Packages\JagexLimited.Dominion_*\SystemAppData\wgs\` | compressés, noms en identifiants, sans extension |

Le dossier `Packages` n'est pas indexé par la recherche Windows. C'est la raison
principale pour laquelle les joueurs Game Pass ne trouvent jamais leurs fichiers.

## Format des sauvegardes

Ce qui suit a été établi par observation de vrais fichiers, et c'est ce sur quoi
l'outil s'appuie.

**Un monde** commence par les quatre octets `53 41 56 45`, soit les lettres `SAVE`.
Suivent un bloc `INFO`, la date de sauvegarde au format ISO en clair, puis la table
des champs du monde : `WorldName`, `WorldMapName`, `FriendlyFire`,
`SurvivalDifficulty`, `HardcoreState`, `TimeOfSave`, `SessionPrivacy`,
`SessionPasswd`, `CrossplayEnabled`, `WorldOwnerId`.

**Le nom du monde** est la dernière chaîne lisible avant le marqueur `L_World`. La
table des champs ne peut pas servir à le lire : c'est une liste de noms de champs, où
`WorldName` est suivi de `WorldMapName` et non d'une valeur.

**Un personnage** est un document JSON qui commence par `{"Version": 83,` et contient
un bloc `meta_data`.

**Côté Game Pass**, le fichier est précédé d'un en-tête de 12 octets, puis vient un
flux **zlib** (`78 9C`). L'outil cherche cette signature dans les 64 premiers octets
au lieu de supposer une position fixe, pour résister à un changement de format.

**Ce qui n'est pas décodé** : les valeurs des réglages du monde. Leurs noms sont
lisibles, mais les valeurs sont stockées ailleurs et référencent la table par index.
Les décoder demanderait de la rétro-ingénierie sur tout le format de sérialisation, et
casserait à chaque mise à jour du jeu. L'outil ne prétend donc pas les lire.

## Importer un monde sur un serveur dédié

Le bouton **Exporter pour un serveur** prépare un dossier contenant le `.sav`
correctement nommé et un `LISEZ-MOI.txt` avec la marche à suivre. En résumé :

1. Arrêter le serveur
2. Vider son dossier `SaveGames`, sans supprimer le dossier lui-même
3. Y copier le `.sav`, **sans le renommer**
4. Mettre `DefaultWorldName` dans `DedicatedServer.ini` sur le nom du monde
5. Redémarrer

Deux pièges qui coûtent cher :

- **Ne jamais renommer un `.sav`.** Le nom du fichier est le nom du monde ; le changer
  fait perdre la progression.
- **Ne jamais modifier `DedicatedServer.ini` pendant que le serveur tourne.** Les
  modifications sont perdues au redémarrage.

Bon à savoir : les réglages du monde (difficulté, inventaire gardé à la mort, tir
allié) sont enregistrés **dans le `.sav`**, pas dans la configuration du serveur.
Importer son monde est donc le seul moyen de les retrouver sur un serveur dédié.

## Compiler soi-même

Nécessite le SDK .NET 8.

```bash
git clone https://github.com/KaineOfficial/FoundMySave.git
cd FoundMySave
dotnet build -c Release
dotnet run --project tests/FoundMySave.Check   # vérifie le moteur sur vos propres sauvegardes
dotnet publish src/FoundMySave -c Release -o publish
```

`FoundMySave.Check` est un outil en console qui parcourt vos sauvegardes, affiche ce
qu'il reconnaît, et contrôle que tout est cohérent. Il inclut un auto-test qui
fabrique un fichier au format Game Pass et vérifie qu'il est restitué à l'identique,
ce qui permet de valider cette partie sans posséder la version Game Pass.

## Organisation du code

```
src/FoundMySave.Core/     lecture, décompression, détection  (sans interface, testable)
src/FoundMySave/          interface WPF
tests/FoundMySave.Check/  vérification en console + auto-test
```

## Licence

MIT. Voir [LICENSE](LICENSE).

Projet indépendant, sans lien avec Jagex. RuneScape et Dragonwilds appartiennent à
leurs propriétaires respectifs.
