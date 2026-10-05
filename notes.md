# Réponses aux questions d'analyse du fichier `docker-compose.yml`

## Q1. Le fichier déclare quatre services alors que le TP d'aujourd'hui n'en utilise qu'un. Pourquoi, à votre avis ?

- **Continuité pédagogique et fil conducteur :** Le projet sert de fil conducteur pour plusieurs séances de TP abordant l'architecture de **persistance polyglotte** (*Polyglot Persistence*). Les commentaires du fichier le confirment :
  - **PostgreSQL** : données transactionnelles (séance 1)
  - **MongoDB** : catalogue de jeux (séance 2)
  - **Redis** : classements, matchmaking, cache (séance 3)
  - **Neo4j** : graphe social et recommandation (séance 4)
- **Simplicité de configuration :** Définir dès le départ l'ensemble de la stack évite d'avoir à modifier la configuration Docker à chaque séance. Les étudiants disposent ainsi immédiatement de l'environnement complet et cohérent pour l'ensemble du module.

---

## Q2. Trois services ont une section volumes, un seul n'en a pas. Lequel, et qu'est-ce que ça implique pour ses données ?

- **Le service concerné :** Il s'agit de **Redis** (`pixelhub-redis`).
- **Implications pour ses données :**
  - Sans volume monté sur l'hôte, les données stockées dans Redis sont **entièrement volatiles**.
  - Si le conteneur est supprimé (par exemple avec `docker compose down`), toutes les données en cache ou clés stockées sont définitivement perdues.
  - Cela reste cohérent avec son rôle principal (cache, classements éphémères, données de session en mémoire), contrairement aux autres bases (Postgres, Mongo, Neo4j) dont les données doivent persister au-delà du cycle de vie des conteneurs grâce à leurs volumes nommés respectifs (`pg_data`, `mongo_data`, `neo4j_data`).

---

## Q3. Que signifie la ligne `"15432:5432"` ? Les deux nombres ne désignent pas la même chose.

La syntaxe Docker pour la redirection de ports est sous la forme `"<port_hôte>:<port_conteneur>"` :
- **`15432` (Port hôte) :** C'est le port ouvert sur votre propre machine (l'hôte). Comme indiqué en tête du fichier compose, ce port est décalé par rapport au port standard (5432) afin d'éviter tout conflit avec un service PostgreSQL qui tournerait déjà localement sur la machine physique de l'étudiant.
- **`5432` (Port conteneur) :** C'est le port d'écoute par défaut du démon PostgreSQL à l'intérieur du conteneur Docker.

**Signification :** Docker redirige les connexions arrivant sur le port `15432` de votre machine hôte vers le port `5432` du conteneur PostgreSQL.

---

## Q4. Les mots de passe sont écrits en clair dans le fichier. Est-ce acceptable ici ? Le serait-ce sur un serveur de production ?

- **Acceptable ici (en TP / développement local) :**
  - **Oui**, car il s'agit d'un environnement d'apprentissage local avec des données de test/factices, sans exposition publique sur Internet.
  - Cela simplifie la prise en main par les étudiants sans nécessiter la mise en place d'une infrastructure complexe de gestion des secrets.
- **En production :**
  - **Absolument non.** Les fichiers de configuration comme `docker-compose.yml` sont généralement archivés sur des dépôts de code (ex. Git). Laisser des identifiants en clair expose le système à des fuites de données et à des compromissions directes.
  - En production, on utilise des outils dédiés à la gestion des secrets (tels que **Docker Secrets**, **Kubernetes Secrets**, **HashiCorp Vault**, ou des variables d'environnement injectées de façon sécurisée via un coffre-fort de secrets ou un pipeline CI/CD).

---

## Q5. Les commandes ci-dessus utilisent `docker compose exec`. Qu'est-ce que ça fait exactement ? Où s'exécute la commande `redis-cli` ?

- **Rôle de `docker compose exec` :**
  - Cette commande permet d'exécuter un processus ou une commande arbitraire **à l'intérieur d'un conteneur déjà en cours d'exécution** (*running*). Docker Compose identifie le conteneur associé au nom du service (ex: `redis`, `postgres`, `mongo`) et transmet la commande au démon Docker pour exécution dans l'environnement isolé de ce conteneur.
- **Où s'exécute `redis-cli` ?**
  - La commande `redis-cli` s'exécute **à l'intérieur du conteneur `pixelhub-redis`** (dans son propre système de fichiers et son espace de nommage réseau/processus), et **non sur la machine hôte**.
  - **Avantage pratique :** Cela évite d'avoir à installer les outils d'administration ou clients en ligne de commande (`redis-cli`, `mongosh`, `pg_isready`, etc.) sur son système d'exploitation hôte : on exploite directement les binaires embarqués dans l'image Docker du service.

---

## Q6. La base de données tourne dans un conteneur Docker, et la chaîne de connexion dit `Host=localhost`. Pourquoi est-ce que ça fonctionne ?

- **Mécanisme de redirection de port (Port Binding) :**
  - Dans le fichier `docker-compose.yml`, la section `ports` de PostgreSQL déclare `"- \"${PG_PORT:-15432}:5432\""`.
  - Docker mappe ainsi le port d'écoute interne `5432` du conteneur sur le port `15432` de l'interface réseau de la **machine hôte** (`localhost`).
  - Lorsque l'API s'exécute sur votre machine et tente de se connecter à `Host=localhost;Port=15432`, elle envoie ses requêtes au port local de l'hôte, que le démon Docker intercepte et achemine fidèlement vers le port `5432` du conteneur PostgreSQL.

---

## Q7. Arrêtez l'application (Ctrl+C), relancez-la. Les trois joueurs sont-ils dupliqués ? Pourquoi ?

- **Non, les trois joueurs ne sont pas dupliqués.**
- **Pourquoi ? (Explication dans le code et la persistance) :**
  1. **Condition d'initialisation dans `Program.cs` :**
     L'insertion des trois joueurs est encapsulée dans une condition :
     ```csharp
     if (!db.Players.Any())
     {
         db.Players.AddRange(...);
         db.SaveChanges();
     }
     ```
     Avant d'insérer, EF Core exécute une requête `SELECT EXISTS(...)` sur la table `Players`. Dès lors que la table contient déjà des enregistrements, `db.Players.Any()` retourne `true` (donc `!db.Players.Any()` est `false`), et l'insertion initiale est ignorée.
  2. **Persistance des données via le volume Docker :**
     Comme PostgreSQL utilise un volume nommé (`pg_data`), les lignes insérées lors du premier démarrage restent écrites et persistées sur le disque même après l'arrêt ou le redémarrage de l'API.




---
---

# TP 2 — Le catalogue de jeux dans MongoDB

## Q1. Vous retrouvez le champ `_id`, déjà aperçu au TP 1 (étape 6.2), que personne n'a écrit dans `games.json`. Cette fois, intéressez-vous à qui l'a fabriqué : le serveur MongoDB, ou l'outil qui a envoyé les documents (`mongoimport`) ? Comparez avec l'`Id` 4 de *Pixel* au TP 1 (Q8) : qui l'avait choisi, et à quel moment le connaissiez-vous ?

- **Qui fabrique l'`_id` dans MongoDB :**
  - C'est **l'outil client** (`mongoimport` ici, ou le driver MongoDB en C#) qui génère l'`ObjectId` côté client avant de transmettre le document au serveur. La structure même d'un `ObjectId` (4 octets de timestamp, 5 octets aléatoires propres à la machine/processus, et 3 octets de compteur) a été pensée pour être générée de façon décentralisée sans risque de collision.
- **Comparaison avec l'`Id` 4 de *Pixel* (PostgreSQL / TP 1) :
  - Dans PostgreSQL, la clé primaire était générée par une séquence auto-incrémentée (`IDENTITY` / `serial`) gérée par le **serveur PostgreSQL**.
  - L'identifiant est attribué uniquement au moment où l'`INSERT` s'exécute sur le serveur. On ne le connaît donc qu'**après** l'écriture en base (via le résultat SQL ou après `SaveChanges()`). Avec MongoDB, l'identifiant peut être connu et manipulé côté client avant même l'écriture.

---

## Q2. À l'exercice 5, `plateformes` est un tableau et vous avez écrit `{ plateformes: "Switch" }`, sans opérateur particulier. Comment auriez-vous fait la même chose en SQL, avec un modèle relationnel normalisé ?

- En SQL normalisé (3NF), on ne peut pas stocker de liste multivaluée directement dans une colonne atomique (1ère Forme Normale).
- Il aurait fallu décomposer le modèle en **trois tables** :
  1. Une table `jeux (id, titre, ...)`
  2. Une table `plateformes (id, nom)`
  3. Une table associative / de jointure `jeux_plateformes (jeu_id, plateforme_id)`
- La requête aurait alors nécessité une **jointure** (`JOIN`) :
  ```sql
  SELECT j.titre
  FROM jeux j
  JOIN jeux_plateformes jp ON j.id = jp.jeu_id
  JOIN plateformes p ON jp.plateforme_id = p.id
  WHERE p.nom = 'Switch';
  ```
- **Apport de MongoDB :** Le support natif des tableaux permet d'interroger directement les éléments d'une liste sans jointure relationnelle.

---

## Q3. Vous tapez `--authenticationDatabase admin` depuis le TP 1 sans qu'on vous ait dit pourquoi. Pourquoi faut-il l'ajouter aux commandes `mongoimport` et `mongosh` ? Où se trouve le compte `pixelhub` ?

- Dans MongoDB, les comptes utilisateurs sont associés et stockés dans une base de données d'authentification spécifique.
- Le compte utilisateur `pixelhub` a été créé dans la base système **`admin`** (lors de l'initialisation du conteneur via `MONGO_INITDB_ROOT_USERNAME` dans `docker-compose.yml`).
- Par défaut, si l'on ne précise pas `--authenticationDatabase`, MongoDB cherche les identifiants dans la base cible à laquelle on se connecte (`pixelhub`), où ce compte n'existe pas, provoquant un échec d'authentification (`MongoAuthenticationException`).
- L'option `--authenticationDatabase admin` indique au serveur de vérifier les credentials contre la base `admin`.

---

## Q4. Personne ne vous a arrêté. Est-ce une bonne nouvelle ou un problème ? Argumentez en imaginant un projet à quatre développeurs, six mois plus tard.

- **Une bonne nouvelle à court terme (flexibilité & prototypage) :**
  - Très grande agilité : aucune migration DDL bloquante pour ajouter des champs.
  - Représentation naturelle de documents polymorphes ou hétérogènes (ex: un FPS stocke `joueursParEquipe` et `cartes`, un jeu de gestion stocke `dureeMoyennePartieHeures`).
- **Un problème majeur à terme en équipe :**
  - Dans un projet à quatre développeurs après six mois, l'absence de contrainte de schéma (*schema-less* par défaut) peut entraîner de graves dérives :
    - Fautes de frappe dans les clés (`nbNiveaux` vs `nombreNiveaux`) non détectées.
    - Types de données incohérents (chaîne dans un document, entier dans un autre).
    - Champs orphelins ou données abandonnées sans documentation.
  - Côté applicatif C#, la désérialisation échouera (`FormatException`) ou nécessitera un surcroît de code défensif.
  - En production, il est donc indispensable de poser des règles de validation de schéma (ex. *JSON Schema Validation* dans MongoDB) et des conventions d'équipe.

---

## Q5. Le résultat de l'exercice 9 vous surprend-il ? Que s'est-il passé exactement, et pourquoi n'y a-t-il eu aucun message d'erreur ?

- **Ce qui s'est passé :**
  - MongoDB est **sensible à la casse** : la collection `jeux` est différente de `Jeux`.
  - MongoDB utilise une approche de création à la demande (*lazy*) : une lecture `find()` sur une collection inexistante n'est pas considérée comme une erreur. MongoDB consulte la collection demandée, constate qu'elle ne contient aucun document correspondant au filtre, et renvoie un curseur vide (`[]`).
  - Aucun message d'erreur n'est émis car rechercher dans une collection vide est une opération syntaxiquement et logiquement valide pour le moteur.
  - *(Remarque : un `insertOne` sur `db.Jeux` aurait créé silencieusement une deuxième collection `Jeux` distincte).*

---

## Q6. L'exercice 16 échoue. Recopiez le message d'erreur, et expliquez pourquoi MongoDB refuse. Quelle commande utiliseriez-vous si vous vouliez réellement remplacer le document en entier ?

- **Message d'erreur obtenu :**
  ```text
  MongoInvalidArgumentError: Update document requires atomic operators
  ```
- **Pourquoi MongoDB refuse :**
  - La méthode `updateOne` est dédiée aux modifications partielles de documents et exige obligatoirement des opérateurs atomiques (tels que `$set`, `$inc`, `$push`, `$unset`).
  - Sans opérateur comme `$set`, passer `{ note: 4.4 }` risquerait d'écraser par accident l'intégralité du document (en supprimant tous ses autres champs à l'exception de l'`_id`). Le driver bloque donc l'appel par sécurité.
- **Commande pour réellement remplacer le document :**
  - Pour remplacer un document dans sa globalité, MongoDB impose d'utiliser explicitement la méthode `replaceOne` :
    ```javascript
    db.jeux.replaceOne({ titre: "Valorant" }, { titre: "Valorant", note: 4.4, genre: "FPS" })
    ```

---

## Q7. À l'exercice 12, vous avez ajouté un champ `nbVotes` à un seul document, alors que les dix autres n'en ont pas. Quelle commande SQL aurait été nécessaire pour faire l'équivalent en relationnel, et quelle en aurait été la conséquence sur les autres lignes ?

- **Commandes SQL nécessaires :**
  1. Modifier le schéma de la table pour ajouter la colonne :
     ```sql
     ALTER TABLE jeux ADD COLUMN nb_votes INTEGER;
     ```
  2. Mettre à jour la ligne ciblée :
     ```sql
     UPDATE jeux SET nb_votes = 0 WHERE titre = 'Terraria';
     ```
- **Conséquence sur les autres lignes :**
  - En modèle relationnel à schéma fixe, la nouvelle colonne est ajoutée à **toutes les lignes existantes** de la table.
  - Toutes les autres lignes reçoivent obligatoirement la valeur `NULL` (ou la valeur par défaut définie), consommant de l'espace de stockage et imposant la gestion des valeurs nulles dans l'application, même si ce champ n'a aucun sens pour d'autres jeux. En MongoDB, le champ n'existe que sur le document modifié.

---

## Q8. Lisez `IGameCatalog`. Y a-t-il un seul endroit dans cette interface où le mot « Mongo » apparaît ? Pourquoi est-ce important, à votre avis ?

- **Constat :**
  - Non, le terme « Mongo » n'apparaît à aucun endroit dans `IGameCatalog`. L'interface ne manipule que des abstractions C# et le modèle métier `Game` (`Task<List<Game>>`, `string genre`, `int count`, etc.).
- **Pourquoi c'est important :**
  - **Principe d'Inversion des Dépendances (DIP) / Clean Architecture :** Les couches supérieures (les endpoints d'API dans `Program.cs`) dépendent d'un contrat (l'interface) et non d'une implémentation concrète.
  - **Découplage technologique :** L'application ne sait pas comment ni où sont stockés les jeux. Si on décide plus tard de remplacer MongoDB par PostgreSQL, un cache Redis, ou un service mocké pour des tests unitaires, l'interface `IGameCatalog` et les endpoints restent inchangés. Seule la classe d'implémentation injectée dans le conteneur IoC est modifiée.


---

## Q9. Quelle exception obtenez-vous, et sur quel champ ? Expliquez la cause : qu'est-ce que le driver a essayé de faire, et pourquoi n'a-t-il pas su ?

- **Exception obtenue :**
  ```text
  System.FormatException: Element 'joueursParEquipe' does not match any field or property of class PixelHub.Api.Models.Game.
  ```
- **Sur quel champ :** Le champ `joueursParEquipe` (présent dans le document de *Counter-Strike 2*).
- **Cause :**
  - Le driver C# MongoDB tente de désérialiser le document BSON retourné par MongoDB vers une instance de la classe `Game`.
  - Par défaut, le sérialiseur BSON C# applique une politique stricte : chaque champ présent dans le document BSON doit posséder une propriété ou un champ correspondant dans la classe C#.
  - Dès qu'il rencontre un champ non déclaré dans la classe `Game` (`joueursParEquipe`), le driver ne sait pas où stocker cette valeur et lève immédiatement une `FormatException` pour signaler une incohérence entre le document et le modèle.

---

## Q10. `[BsonIgnoreExtraElements]` fait disparaître l'erreur, mais au prix de quoi ? Que devient `joueursParEquipe` quand vous appelez `/games` ?

- **Au prix de quoi :**
  - Cela se fait au prix d'une **perte d'information** au niveau applicatif. Tout champ présent dans MongoDB mais non déclaré dans la classe C# est silencieusement ignoré et jeté lors de la désérialisation.
- **Que devient `joueursParEquipe` :**
  - Le champ `joueursParEquipe` n'est pas chargé en mémoire dans l'objet `Game`.
  - Par conséquent, il est totalement absent du flux JSON retourné par l'endpoint `/games`.

---

## Q11. Comparez les trois approches possibles pour gérer un schéma variable en C# : `[BsonIgnoreExtraElements]`, `[BsonExtraElements]`, et une hiérarchie de classes (`FpsGame : Game`, etc.). Quel est l'avantage et l'inconvénient de chacune ?

1. **`[BsonIgnoreExtraElements]`** :
   - **Avantage :** Solution la plus simple et directe. L'application reste stable face aux variations de schéma et ne mappe que le sous-ensemble commun de champs dont elle a besoin.
   - **Inconvénient :** Perte de données : les champs spécifiques aux documents ne peuvent pas être exploités, modifiés ou réécrits par l'application.
2. **`[BsonExtraElements]` (avec un dictionnaire / BsonDocument dynamique)** :
   - **Avantage :** Conserve toutes les données non déclarées sans perte. Permet d'exposer dynamiquement les champs spécifiques dans l'API (via un dictionnaire).
   - **Inconvénient :** Perte du typage fort C# sur ces champs (manipulation de clés/valeurs génériques `object` ou `BsonDocument`), et complexité de conversion entre la sérialisation BSON et le sérialiseur JSON de l'API (`[JsonIgnore]`, `ToDictionary()`).
3. **Hiérarchie de classes (`FpsGame : Game`, `GestionGame : Game`, avec discriminateur BSON)** :
   - **Avantage :** Typage fort complet, propreté du POCO, validation à la compilation et autocomplétion pour chaque famille de jeu.
   - **Inconvénient :** Rigidité accrue : chaque nouvelle variante nécessite de coder et maintenir une nouvelle classe C#. Moins adapté si les documents évoluent continuellement ou sont très imprévisibles.

---

## Q12. Écrivez la requête SQL qui produirait le même résultat sur une table `jeux(titre, genre, note)`. Comparez les deux. Est-ce ici que MongoDB apporte quelque chose par rapport à PostgreSQL ? Si non, où l'apport se situe-t-il dans ce TP ?

- **Requête SQL équivalente :**
  ```sql
  SELECT genre, AVG(note) AS "NoteMoyenne", COUNT(*) AS "Nombre"
  FROM jeux
  GROUP BY genre
  ORDER BY "NoteMoyenne" DESC;
  ```
- **Comparaison :**
  - La requête SQL est plus lisible, déclarative et naturelle pour effectuer des calculs statistiques groupés (`GROUP BY`, `AVG`, `COUNT`).
  - Le pipeline d'agrégation MongoDB en C# (`Aggregate().Group(...).SortByDescending(...)`) est plus impératif et verbeux.
- **MongoDB apporte-t-il quelque chose ici ?**
  - **Non**, sur les agrégations tabulaires classiques, MongoDB n'apporte rien de supérieur à PostgreSQL, qui dispose d'un optimiseur de requêtes relationnelles extrêmement performant et éprouvé.
  - **Où se situe le réel apport de MongoDB dans ce TP :**
    1. **Le polymorphisme / schéma flexible :** Pouvoir stocker des jeux aux attributs très disparates dans une même collection sans colonnes `NULL` superflues.
    2. **Les types complexes et collections imbriquées :** La gestion native des tableaux (`plateformes`, `tags`, `cartes`) sans tables de jointure n:m.

---

## Question orale de l'enseignant : *« Les avis d'un jeu : tu imbriques ou tu référencies ? »*

- **Réponse argumentée :**
  - **On imbrique (Embedding) si :**
    - Le nombre d'avis reste modéré et borné (pour ne pas risquer d'atteindre la limite technique de 16 Mo par document BSON).
    - Les avis sont quasi systématiquement affichés en même temps que la fiche détaillée du jeu (lecture conjointe fréquente en 1 seule opération d'E/S).
  - **On référence (Referencing avec une collection `avis` distincte) si :**
    - Le volume d'avis peut exploser (des milliers d'avis par jeu), ce qui alourdirait le document `Game` lors des requêtes simples (comme lister les jeux du catalogue).
    - Les avis ont leur propre cycle de vie (pagination, votes sur les avis, modération indépendante, analyse statistique globale des utilisateurs).
  - **Conclusion :** Dans un système réaliste comme PixelHub avec une communauté active, le **référencement** (ou une approche hybride avec les derniers avis imbriqués et l'historique archivé/référencé) est généralement préférable.


---
---

# TP 2 — Pour aller plus loin (Bonus)

## 1. Index et mesure (`explain("executionStats")`)

- **Avant la création de l'index :**
  - Requête : `db.jeux.find({ genre: "FPS" }).explain("executionStats")`
  - Étape d'exécution : `COLLSCAN` (Collection Scan, parcours séquentiel complet).
  - Documents examinés : `totalDocsExamined = 11`.
  - Documents retournés : `nReturned = 2`.
  - Temps d'exécution : ~21 ms.
- **Création de l'index :**
  ```javascript
  db.jeux.createIndex({ genre: 1 })
  ```
- **Après la création de l'index :**
  - Étape d'exécution : `IXSCAN` (Index Scan) suivi de `FETCH`.
  - Clés d'index examinées : `totalKeysExamined = 2`.
  - Documents examinés : `totalDocsExamined = 2`.
  - Documents retournés : `nReturned = 2`.
  - Temps d'exécution : ~8 ms.
- **Constat sur 10 documents et explication :**
  - Sur un jeu de données aussi réduit (10-11 documents), le gain réel est imperceptible (le parcours complet en RAM est instantané). L'index introduit même une structure mémoire supplémentaire et un léger surcoût à l'écriture.
  - En revanche, sur une base de production contenant plusieurs millions de jeux, l'index est indispensable : il transforme une recherche linéaire en $O(N)$ (qui saturerait le CPU et le disque) en une recherche logarithmique en $O(\log N)$.

---

## 2. Modéliser les avis (Documents imbriqués)

- **Ajout des avis en imbriqué :**
  ```javascript
  db.jeux.updateOne(
    { titre: "Baldur's Gate 3" },
    {
      $set: {
        avis: [
          { auteur: "Alice", note: 5, commentaire: "Chef d'œuvre absolu !" },
          { auteur: "Bob", note: 4, commentaire: "Très bon mais chronophage." }
        ]
      }
    }
  );
  ```
- **Requête renvoyant les jeux ayant au moins un avis avec la note 5 :**
  ```javascript
  db.jeux.find({ "avis.note": 5 }, { titre: 1, avis: 1, _id: 0 })
  ```
  *(Ou avec `$elemMatch` si l'on a plusieurs critères sur le même sous-document : `db.jeux.find({ avis: { $elemMatch: { note: 5 } } })`)*.
- **Résultat :** Renvoie bien *Baldur's Gate 3*. Grâce au modèle orienté document, on filtre directement à l'intérieur d'un tableau d'objets imbriqués sans jointure.

---

## 3. Validation de schéma (`$jsonSchema`)

- **Commande pour imposer un champ `titre` de type chaîne obligatoire :**
  ```javascript
  db.runCommand({
    collMod: "jeux",
    validator: {
      $jsonSchema: {
        bsonType: "object",
        required: ["titre"],
        properties: {
          titre: {
            bsonType: "string",
            description: "doit être une chaîne de caractères et est obligatoire"
          }
        }
      }
    },
    validationLevel: "moderate"
  });
  ```
- **Test d'insertion invalide (sans titre) :**
  ```javascript
  db.jeux.insertOne({ genre: "Invalide Sans Titre", note: 1.0 })
  ```
- **Résultat observé :**
  - L'insertion est rejetée avec l'erreur `MongoServerError: Document failed validation`.
  - Détail de l'erreur : `missingProperties: [ 'titre' ]` sur la règle `$jsonSchema`.
  - Cela permet de sécuriser le schéma au niveau du moteur tout en conservant la souplesse de MongoDB sur les autres attributs.

---

## Question d'ouverture pour la séance 3

> *« PixelHub doit afficher le nombre de joueurs actuellement en ligne, mis à jour chaque seconde. Vous le stockez dans PostgreSQL, ou dans MongoDB ? »*

- **Réponse :** Dans **aucun des deux** !
- **Moteur adapté :** On utilise **Redis** (qui sera vu à la séance 3).
- **Pourquoi :**
  - Écrire et mettre à jour un compteur chaque seconde dans PostgreSQL ou MongoDB entraînerait des écritures disque et des verrous transactionnels inutiles pour une donnée très éphémère.
  - Redis stocke toutes ses données en mémoire vive (RAM) et propose des opérations atomiques instantanées en $O(1)$ (comme `INCR` / `DECR`), ce qui en fait le moteur idéal pour les compteurs temps réel et les données de présence.
