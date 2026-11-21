<?php defined( 'ABSPATH' ) || exit; get_header(); ?>
<div class="content-layout"><div>
<?php while ( have_posts() ) : the_post(); ?>
    <article class="release-card"><h1><?php the_title(); ?></h1><div class="entry-content"><?php the_content(); wp_link_pages(); ?></div></article>
<?php endwhile; ?>
</div><?php get_sidebar(); ?></div>
<?php get_footer(); ?>
